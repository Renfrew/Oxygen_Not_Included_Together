using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ONI_United.DebugTools;
using Shared.Profiling;

namespace ONI_United.Networking.Transfer
{
	public class TcpFileTransferServer
	{
		private TcpListener _listener;
		private Thread _acceptThread;
        private volatile bool _running;
        private readonly object _workersLock = new object();
        private readonly HashSet<TcpClient> _clients = new HashSet<TcpClient>();
        private readonly List<Thread> _handlers = new List<Thread>();

        internal bool IsRunning => _running;
        private string _cleanupFailure;
        internal string CleanupFailure { get { lock (_workersLock) return _cleanupFailure; } }

        internal bool IsStopped
        {
            get
            {
                lock (_workersLock)
                {
                    if (_listener != null || _running || _clients.Count != 0 || _acceptThread?.IsAlive == true)
                        return false;
                    foreach (var handler in _handlers)
                        if (handler.IsAlive) return false;
                    return true;
                }
            }
        }
		private readonly ConcurrentDictionary<(ulong ClientId, ulong SynchronizationId), PendingTransfer> _pending =
			new ConcurrentDictionary<(ulong ClientId, ulong SynchronizationId), PendingTransfer>();

		private class PendingTransfer
		{
			public string FileName;
			public byte[] Data;
		}

		public void Start(int lanPort)
		{
			using var _ = Profiler.Scope();

			int tcpPort = lanPort + 1;
			_listener = new TcpListener(IPAddress.Any, tcpPort);
			_listener.Start();
			_running = true;

			_acceptThread = new Thread(AcceptLoop)
			{
				IsBackground = true,
				Name = "TcpFileTransfer_Accept"
			};
			_acceptThread.Start();

			DebugConsole.Log($"[TcpFileTransfer] Server started on port {tcpPort}");
		}

		public void QueueTransfer(ulong clientId, ulong synchronizationId, string fileName, byte[] data)
		{
			using var _ = Profiler.Scope();

			_pending[(clientId, synchronizationId)] = new PendingTransfer { FileName = fileName, Data = data };
			DebugConsole.Log($"[TcpFileTransfer] Queued transfer '{fileName}' ({data.Length} bytes) for client {clientId}");
		}

		public void ClearPendingTransfers()
		{
			_pending.Clear();
		}

		internal void CancelClient(ulong clientId)
		{
			foreach (var key in _pending.Keys)
				if (key.ClientId == clientId)
					_pending.TryRemove(key, out var removed);
		}

        private void AcceptLoop()
        {
            // A stopped server cannot replace this listener under its accept worker.
            var listener = _listener;
            while (_running)
            {
                TcpClient client = null;
                try
                {
                    client = listener.AcceptTcpClient();
                    lock (_workersLock)
                    {
                        _clients.Add(client);
                        if (_running)
                        {
                            var accepted = client;
                            var handler = new Thread(() => HandleClient(accepted))
                            {
                                IsBackground = true,
                                Name = "TcpFileTransfer_Handler"
                            };
                            _handlers.RemoveAll(worker => !worker.IsAlive);
                            _handlers.Add(handler);
                            try { handler.Start(); }
                            catch
                            {
                                _handlers.Remove(handler);
                                throw;
                            }
                            client = null; // The tracked handler now owns this socket.
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_running) DebugConsole.LogError("[TcpFileTransfer] Accept error: " + ex.Message, false);
                }
                finally
                {
                    // Includes a socket accepted concurrently with Stop. Never close under the lock.
                    if (client != null)
                    {
                        try
                        {
                            client.Close();
                            lock (_workersLock) _clients.Remove(client);
                        }
                        catch (Exception ex)
                        {
                            lock (_workersLock) _cleanupFailure = ex.Message;
                            DebugConsole.LogError("[TcpFileTransfer] Socket close failed: " + ex.Message, false);
                        }
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using var _ = Profiler.Scope();
            try
            {
                client.SendBufferSize = 65536;
                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 10000;

                byte[] idBuffer = new byte[16];
                int read = 0;
                while (read < idBuffer.Length)
                {
                    int n = stream.Read(idBuffer, read, idBuffer.Length - read);
                    if (n == 0) throw new IOException("Client disconnected before sending ID");
                    read += n;
                }

                ulong clientId = BitConverter.ToUInt64(idBuffer, 0);
                ulong synchronizationId = BitConverter.ToUInt64(idBuffer, 8);
                DebugConsole.Log($"[TcpFileTransfer] Client {clientId} connected for download");
                if (!_pending.TryRemove((clientId, synchronizationId), out var transfer))
                {
                    DebugConsole.LogWarning($"[TcpFileTransfer] No pending transfer for client {clientId}");
                    return;
                }

                byte[] fileNameBytes = Encoding.UTF8.GetBytes(transfer.FileName);
                byte[] fileNameLenBytes = BitConverter.GetBytes(fileNameBytes.Length);
                byte[] fileSizeBytes = BitConverter.GetBytes(transfer.Data.Length);
                stream.Write(fileNameLenBytes, 0, 4);
                stream.Write(fileNameBytes, 0, fileNameBytes.Length);
                stream.Write(fileSizeBytes, 0, 4);
                stream.Write(transfer.Data, 0, transfer.Data.Length);
                stream.Flush();
                DebugConsole.Log($"[TcpFileTransfer] Sent '{transfer.FileName}' ({transfer.Data.Length} bytes) to client {clientId}");
            }
            catch (Exception ex)
            {
                if (_running) DebugConsole.LogError($"[TcpFileTransfer] Handler error: {ex.Message}", false);
            }
            finally
            {
                try
                {
                    client.Close();
                    lock (_workersLock) _clients.Remove(client);
                }
                catch (Exception ex)
                {
                    lock (_workersLock) _cleanupFailure = ex.Message;
                    DebugConsole.LogError("[TcpFileTransfer] Socket close failed: " + ex.Message, false);
                }
            }
        }

        public void Stop()
		{
			using var _ = Profiler.Scope();

            TcpClient[] clients;
            lock (_workersLock)
            {
                _running = false;
                _cleanupFailure = null; // Only an explicit cleanup attempt resets failure.
                clients = new TcpClient[_clients.Count];
                _clients.CopyTo(clients);
            }
            var errors = new List<Exception>();
            try
            {
                _listener?.Stop();
                _listener = null;
            }
            catch (Exception ex) { errors.Add(ex); }
            foreach (var client in clients)
            {
                try
                {
                    client.Close();
                    lock (_workersLock) _clients.Remove(client);
                }
                catch (Exception ex) { errors.Add(ex); }
            }
            _pending.Clear();
            // No Join: IsStopped is observed by the persistent owner on later Updates.
            if (errors.Count != 0) throw new AggregateException("TCP resource cleanup failed.", errors);
        }
    }
}

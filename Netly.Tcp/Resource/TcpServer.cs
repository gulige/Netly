﻿using Netly.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Netly.Tcp
{
    /// <summary>
    /// TCP: Server
    /// </summary>
    public class TcpServer : ITcpServer
    {
        #region Var

        #region Public

        /// <summary>
        /// Endpoint
        /// </summary>
        public Host Host { get; private set; }

        /// <summary>
        /// Returns true if using encryption like SSL, TLS
        /// </summary>
        public bool IsEncrypted { get; private set; }

        /// <summary>
        /// Returns true if socket is connected
        /// </summary>
        public bool Opened { get => IsOpened(); }

        /// <summary>
        /// Returns list of TcpClient
        /// </summary>
        public List<TcpClient> Clients { get; private set; }

        #endregion

        #region Private

        private Socket _socket;

        private bool _tryOpen;
        private bool _tryClose;
        private bool _invokeClose;
        private bool _opened;
        private readonly object _lock = new object();

        #region Events

        private EventHandler _OnOpen;
        private EventHandler _OnClose;
        private EventHandler<Exception> _OnError;
        private EventHandler<TcpClient> _OnEnter;
        private EventHandler<TcpClient> _OnExit;
        private EventHandler<(TcpClient client, byte[] data)> _OnData;
        private EventHandler<(TcpClient client, string name, byte[] data)> _OnEvent;

        private EventHandler<Socket> _OnBeforeOpen;
        private EventHandler<Socket> _OnAfterOpen;

        #endregion

        #endregion

        #endregion

        #region Builder

        /// <summary>
        /// Creating instance
        /// </summary>
        public TcpServer()
        {
            Host = new Host(IPAddress.Any, 0);
            Clients = new List<TcpClient>();
            _socket = new Socket(Host.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        #endregion

        #region Init

        /// <summary>
        /// Use to open connection
        /// </summary>
        /// <param name="host">Endpoint</param>
        /// <param name="backlog">Backlog</param>
        public void Open(Host host, int backlog = 0)
        {
            if (Opened || _tryOpen || _tryClose) return;

            _tryOpen = true;

            Async.SafePool(() =>
            {
                try
                {
                    _socket = new Socket(host.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _socket.NoDelay = true;
                    
                    if (backlog < 1) backlog = 999;

                    _OnBeforeOpen?.Invoke(this, _socket);

                    _socket.Bind(host.EndPoint);
                    _socket.Listen(backlog);

                    Host = host;

                    _opened = true;
                    _invokeClose = false;

                    _OnAfterOpen?.Invoke(this, _socket);

                    _OnOpen?.Invoke(this, EventArgs.Empty);

                    Async.SafePool(BeginAccept);
                }
                catch (Exception e)
                {
                    _OnError?.Invoke(this, e);
                }

                _tryOpen = false;
            });
        }

        /// <summary>
        /// Use to close connection
        /// </summary>
        public void Close()
        {
            if (!Opened || _tryOpen || _tryClose) return;

            _tryClose = true;

            _socket.Shutdown(SocketShutdown.Both);

            Async.SafePool(() =>
            {
                try
                {
                    _socket.Close();
                    _socket.Dispose();
                }
                finally
                {
                    _socket = null;

                    foreach (TcpClient client in Clients.ToArray())
                    {
                        try
                        {
                            client?.Close();
                        }
                        catch { }
                    }

                    _opened = false;
                    Clients.Clear();

                    if (!_invokeClose)
                    {
                        _invokeClose = true;
                        _OnClose?.Invoke(this, EventArgs.Empty);
                    }
                }

                _tryClose = false;
            });
        }

        private bool IsOpened()
        {
            if (_socket == null) return false;

            return _opened;
        }

        private void BeginAccept()
        {
            while (Opened)
            {
                 try
                 {
                     var clientSocket = _socket.Accept();
                 
                     if (clientSocket == null) continue;
                  
                     EndAccept(clientSocket);
                 }
                 catch { }
            }
        }

        private TcpClient Queue(TcpClient client, bool remove)
        {
            if (client == null) return null;

            lock(_lock)
            {
                if (remove)
                {
                    foreach (TcpClient target in Clients.ToArray())
                    {
                        if (target != null && client.Id == target.Id)
                        {
                            try
                            {
                                Clients.Remove(target);
                                return client;
                            }
                            catch { }
                        }
                    }
                    
                    return null;
                }

                Clients.Add(client);

                return client;
            }
        }

        private void EndAccept(Socket socket)
        {
            TcpClient client = new TcpClient(Guid.NewGuid().ToString(), socket);
            Queue(client, false);

            client.OnOpen(() =>
            {
                _OnEnter?.Invoke(this, client);
            });

            client.OnClose(() =>
            {
                _OnExit?.Invoke(this, Queue(client, true) ?? client);
            });

            client.OnData((data) =>
            {
                _OnData?.Invoke(this, (client, data));
            });

            client.OnEvent((name, data) =>
            {
                _OnEvent?.Invoke(this, (client, name, data));
            });

            client.InitServer();
        }

        /// <summary>
        /// Use to make use of encryption
        /// </summary>
        /// <param name="value">Use encryption?</param>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NotImplementedException"></exception>
        public void UseEncryption(bool value)
        {
            if (Opened)
            {
                throw new Exception("Error, you can't add encryption configuration to an open socket");
            }

            throw new NotImplementedException(nameof(UseEncryption));

            // IsEncrypted = value;
        }

        /// <summary>
        /// Sends raw data to all connected clients
        /// </summary>
        /// <param name="data">The date to be published</param>
        public void BroadcastToData(byte[] data)
        {
            if (!Opened || data == null) return;

            foreach (TcpClient client in Clients.ToArray())
            {
                client.ToData(data);
            }
        }

        /// <summary>
        /// Sends formatted "event" data to all connected clients
        /// </summary>
        /// <param name="name">Event name "subscription"</param>
        /// <param name="data">The date to be published</param>
        public void BroadcastToEvent(string name, byte[] data)
        {
            if (!Opened || data == null) return;

            foreach (TcpClient client in Clients.ToArray())
            {
                client.ToEvent(name, data);
            }
        }

        #endregion

        #region Customization Event

        /// <summary>
        /// Is called, executes action before socket connect
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnBeforeOpen(Action<Socket> callback)
        {
            _OnBeforeOpen += (sender, socket) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke(socket);
                });
            };
        }

        /// <summary>
        /// Is called, executes action after socket connect
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnAfterOpen(Action<Socket> callback)
        {
            _OnAfterOpen += (sender, socket) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke(socket);
                });
            };
        }

        #endregion

        #region Events

        /// <summary>
        ///  Execute the callback, when: the connection is opened
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnOpen(Action callback)
        {
            _OnOpen += (sender, args) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke();
                });
            };
        }

        /// <summary>
        /// Execute the callback, when: the connection is closed
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnClose(Action callback)
        {
            _OnClose += (sender, args) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke();
                });
            };
        }

        /// <summary>
        /// Execute the callback, when: the connection cannot be opened
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnError(Action<Exception> callback)
        {
            _OnError += (sender, exception) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke(exception);
                });
            };
        }

        /// <summary>
        /// Execute the callback, when: a client connects
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnEnter(Action<TcpClient> callback)
        {
            _OnEnter += (sender, client) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke(client);
                });
            };
        }

        /// <summary>
        /// Execute the callback, when: a client disconnects
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnExit(Action<TcpClient> callback)
        {
            _OnExit += (sender, client) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke(client);
                });
            };
        }

        /// <summary>
        /// Execute the callback, when: the connection receives raw data
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnData(Action<TcpClient, byte[]> callback)
        {
            _OnData += (sender, value) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke(value.client, value.data);
                });
            };
        }

        /// <summary>
        /// Execute the callback, when: the connection receives event data
        /// </summary>
        /// <param name="callback">action/callback</param>
        public void OnEvent(Action<TcpClient, string, byte[]> callback)
        {
            _OnEvent += (sender, value) =>
            {
                Call.Execute(() =>
                {
                    callback?.Invoke(value.client, value.name, value.data);
                });
            };
        }

        #endregion
    }
}

/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace BattleNET
{
    public class BattlEyeClient
    {
        // These fields are assigned in ConnectInternal before use.
        private Socket _socket;
        private DateTime _packetSent;
        private DateTime _packetReceived;
        private BattlEyeDisconnectionType? _disconnectionType;
        private bool _keepRunning;
        private int _sequenceNumber;
        private int _currentPacket;
        private SortedDictionary<int, string[]> _packetQueue;
        private BattlEyeLoginCredentials _loginCredentials;

        public bool Connected => _socket != null && _socket.Connected;
        public bool ReconnectOnPacketLoss { get; set; }
        public int CommandQueue => _packetQueue.Count;

        public BattlEyeClient(BattlEyeLoginCredentials loginCredentials)
        {
            _loginCredentials = loginCredentials;
        }

        public BattlEyeConnectionResult Connect()
        {
            return ConnectInternal(100);
        }

        private BattlEyeConnectionResult ConnectInternal(int counter)
        {
            _packetSent = DateTime.Now;
            _packetReceived = DateTime.Now;
            _sequenceNumber = 0;
            _currentPacket = -1;
            _packetQueue = new SortedDictionary<int, string[]>();
            _keepRunning = true;

            var remoteEp = new IPEndPoint(_loginCredentials.Host, _loginCredentials.Port);
            Log.Debug("Connecting socket to remote endpoint: {RemoteEp}", remoteEp);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                ReceiveBufferSize = int.MaxValue,
                ReceiveTimeout = 5000
            };

            try
            {
                _socket.Connect(remoteEp);
                Log.Debug("Socket connected: {Connected}", _socket.Connected);

                // Send login packet and log its result.
                var loginResult = SendLoginPacket(_loginCredentials.Password);
                Log.Debug("SendLoginPacket returned: {LoginResult}", loginResult);
                if (loginResult == BattlEyeCommandResult.Error)
                    return BattlEyeConnectionResult.ConnectionFailed;

                var bytesReceived = new byte[4096];
                int receivedCount = _socket.Receive(bytesReceived, bytesReceived.Length, 0);
                Log.Debug("Received {Count} bytes: {HexDump}", receivedCount, BitConverter.ToString(bytesReceived, 0, receivedCount));

                if (receivedCount > 8)
                {
                    if (bytesReceived[7] == 0x00)
                    {
                        if (bytesReceived[8] == 0x01)
                        {
                            Log.Debug("Login successful: byte[7]=0x00, byte[8]=0x01");
                            OnConnect(_loginCredentials, BattlEyeConnectionResult.Success);
                            Receive();
                        }
                        else
                        {
                            Log.Debug("Invalid login: byte[7]=0x00 but byte[8]={Byte8}", bytesReceived[8]);
                            OnConnect(_loginCredentials, BattlEyeConnectionResult.InvalidLogin);
                            return BattlEyeConnectionResult.InvalidLogin;
                        }
                    }
                    else
                    {
                        Log.Debug("Connection failed: byte[7]={Byte7}", bytesReceived[7]);
                        return BattlEyeConnectionResult.ConnectionFailed;
                    }
                }
                else
                {
                    Log.Debug("Insufficient data received: only {Count} bytes", receivedCount);
                    return BattlEyeConnectionResult.ConnectionFailed;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in ConnectInternal");
                if (_disconnectionType == BattlEyeDisconnectionType.ConnectionLost)
                {
                    Disconnect(BattlEyeDisconnectionType.ConnectionLost);
                    if (counter > 0)
                    {
                        return ConnectInternal(counter - 1);
                    }
                    return BattlEyeConnectionResult.ConnectionFailed;
                }
                else
                {
                    OnConnect(_loginCredentials, BattlEyeConnectionResult.ConnectionFailed);
                    return BattlEyeConnectionResult.ConnectionFailed;
                }
            }

            return BattlEyeConnectionResult.Success;
        }

        private BattlEyeCommandResult SendLoginPacket(string command)
        {
            try
            {
                if (!_socket.Connected)
                    return BattlEyeCommandResult.NotConnected;

                byte[] packet = ConstructPacket(BattlEyePacketType.Login, 0, command);
                Log.Debug("Sending login packet: {PacketHex}", BitConverter.ToString(packet));
                _socket.Send(packet);
                _packetSent = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in SendLoginPacket");
                return BattlEyeCommandResult.Error;
            }

            return BattlEyeCommandResult.Success;
        }

        private BattlEyeCommandResult SendAcknowledgePacket(string command)
        {
            try
            {
                if (!_socket.Connected)
                    return BattlEyeCommandResult.NotConnected;

                byte[] packet = ConstructPacket(BattlEyePacketType.Acknowledge, 0, command);
                Log.Debug("Sending acknowledge packet: {PacketHex}", BitConverter.ToString(packet));
                _socket.Send(packet);
                _packetSent = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in SendAcknowledgePacket");
                return BattlEyeCommandResult.Error;
            }

            return BattlEyeCommandResult.Success;
        }

        public int SendCommand(string command, bool log = true)
        {
            return SendCommandPacket(command, log);
        }

        private int SendCommandPacket(string? command, bool log = true)
        {
            command ??= string.Empty;
            int packetID = _sequenceNumber;
            _sequenceNumber = (_sequenceNumber == 255) ? 0 : _sequenceNumber + 1;

            try
            {
                if (!_socket.Connected)
                    return 256;

                var packet = ConstructPacket(BattlEyePacketType.Command, packetID, command);
                _packetSent = DateTime.Now;

                if (log)
                {
                    _packetQueue.Add(packetID, new[] { command, _packetSent.ToString(CultureInfo.InvariantCulture) });
                }
                else
                {
                    SendPacket(packet);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in SendCommandPacket");
                return 256;
            }

            return packetID;
        }

        public int SendCommand(BattlEyeCommand command, string parameters = "")
        {
            return SendCommandPacket(command, parameters);
        }

        private int SendCommandPacket(BattlEyeCommand command, string parameters = "")
        {
            int packetID = _sequenceNumber;
            _sequenceNumber = (_sequenceNumber == 255) ? 0 : _sequenceNumber + 1;

            try
            {
                if (!_socket.Connected)
                    return 256;

                ConstructPacket(BattlEyePacketType.Command, packetID, Helpers.StringValueOf(command) + parameters);
                _packetSent = DateTime.Now;
                _packetQueue.Add(packetID, new[] { Helpers.StringValueOf(command) + parameters, _packetSent.ToString(CultureInfo.InvariantCulture) });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in SendCommandPacket (enum version)");
                return 256;
            }

            return packetID;
        }

        private void SendPacket(byte[] packet)
        {
            _socket.Send(packet);
        }

        private byte[] ConstructPacket(BattlEyePacketType packetType, int sequenceNumber, string command)
        {
            string type;
            switch (packetType)
            {
                case BattlEyePacketType.Login:
                    type = Helpers.Hex2Ascii("FF00");
                    break;
                case BattlEyePacketType.Command:
                    type = Helpers.Hex2Ascii("FF01");
                    break;
                case BattlEyePacketType.Acknowledge:
                    type = Helpers.Hex2Ascii("FF02");
                    break;
                default:
                    return Array.Empty<byte>();
            }

            if (packetType != BattlEyePacketType.Acknowledge)
            {
                command = Encoding.GetEncoding(1252).GetString(Encoding.UTF8.GetBytes(command));
            }

            var count = Helpers.Bytes2String(new[] { (byte)sequenceNumber });
            var byteArray = new CRC32().ComputeHash(Helpers.String2Bytes(type + ((packetType != BattlEyePacketType.Command) ? "" : count) + command));
            var hash = new string(Helpers.Hex2Ascii(BitConverter.ToString(byteArray).Replace("-", "")).ToCharArray().Reverse().ToArray());
            var packet = "BE" + hash + type + ((packetType != BattlEyePacketType.Command) ? "" : count) + command;
            return Helpers.String2Bytes(packet);
        }

        public void Disconnect()
        {
            _keepRunning = false;
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            OnDisconnect(_loginCredentials, BattlEyeDisconnectionType.Manual);
        }

        private void Disconnect(BattlEyeDisconnectionType? disconnectionType)
        {
            if (disconnectionType == BattlEyeDisconnectionType.ConnectionLost)
                _disconnectionType = BattlEyeDisconnectionType.ConnectionLost;
            _keepRunning = false;
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            if (disconnectionType != null)
                OnDisconnect(_loginCredentials, disconnectionType);
        }

        private async void Receive()
        {
            var state = new StateObject(_socket);
            _disconnectionType = null;
            _socket.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);

            while (_socket.Connected && _keepRunning)
            {
                int timeoutClient = (int)(DateTime.Now - _packetSent).TotalSeconds;
                int timeoutServer = (int)(DateTime.Now - _packetReceived).TotalSeconds;

                if (timeoutClient >= 5)
                {
                    if (timeoutServer >= 20)
                    {
                        Disconnect(BattlEyeDisconnectionType.ConnectionLost);
                        _keepRunning = true;
                    }
                    else
                    {
                        if (_packetQueue.Count == 0)
                        {
                            SendCommandPacket(string.Empty, false);
                        }
                    }
                }

                if (_socket.Connected && _packetQueue.Count > 0 && _socket.Available == 0)
                {
                    try
                    {
                        int key = _packetQueue.First().Key;
                        if (_currentPacket == -1 || !_packetQueue.ContainsKey(_currentPacket))
                        {
                            _currentPacket = key;
                            string value = _packetQueue[key][0];
                            SendPacket(ConstructPacket(BattlEyePacketType.Command, key, value));
                        }
                    }
                    catch
                    {
                        // Prevent possible crash when a packet is received at the same moment it's trying to resend it.
                    }
                }

                await Task.Delay(250);
            }

            if (!_socket.Connected)
            {
                if (ReconnectOnPacketLoss && _keepRunning)
                {
                    Connect();
                }
                else if (!_keepRunning)
                {
                    // let the thread finish without further action
                }
                else
                {
                    OnDisconnect(_loginCredentials, BattlEyeDisconnectionType.ConnectionLost);
                }
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState!;
                Socket client = state.WorkSocket;
                if (!client.Connected)
                {
                    return;
                }
                int bytesRead = client.EndReceive(ar);
                if (state.Buffer[7] == 0x02)
                {
                    SendAcknowledgePacket(Helpers.Bytes2String(new[] { state.Buffer[8] }));
                    OnBattlEyeMessage(Helpers.Bytes2String(state.Buffer, 9, bytesRead - 9), 256);
                }
                else if (state.Buffer[7] == 0x01)
                {
                    if (bytesRead > 9)
                    {
                        if (state.Buffer[7] == 0x01 && state.Buffer[9] == 0x00)
                        {
                            if (state.Buffer[11] == 0)
                            {
                                state.PacketsTodo = state.Buffer[10];
                            }
                            if (state.PacketsTodo > 0)
                            {
                                state.Message.Append(Helpers.Bytes2String(state.Buffer, 12, bytesRead - 12));
                                state.PacketsTodo--;
                            }
                            if (state.PacketsTodo == 0)
                            {
                                OnBattlEyeMessage(state.Message.ToString(), state.Buffer[8]);
                                state.Message = new StringBuilder();
                                state.PacketsTodo = 0;
                            }
                        }
                        else
                        {
                            state.Message = new StringBuilder();
                            state.PacketsTodo = 0;
                            OnBattlEyeMessage(Helpers.Bytes2String(state.Buffer, 9, bytesRead - 9), state.Buffer[8]);
                        }
                    }
                    if (_packetQueue != null && _packetQueue.ContainsKey(state.Buffer[8]) && state.PacketsTodo == 0)
                    {
                        _packetQueue.Remove(state.Buffer[8]);
                    }
                }
                _packetReceived = DateTime.Now;
                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
            }
            catch
            {
                // Do nothing.
            }
        }

        private void OnBattlEyeMessage(string message, int id)
        {
            BattlEyeMessageReceived?.Invoke(new BattlEyeMessageEventArgs(message, id));
        }

        private void OnConnect(BattlEyeLoginCredentials loginDetails, BattlEyeConnectionResult connectionResult)
        {
            if (connectionResult == BattlEyeConnectionResult.ConnectionFailed || connectionResult == BattlEyeConnectionResult.InvalidLogin)
                Disconnect(null);
            BattlEyeConnected?.Invoke(new BattlEyeConnectEventArgs(loginDetails, connectionResult));
        }

        private void OnDisconnect(BattlEyeLoginCredentials loginDetails, BattlEyeDisconnectionType? disconnectionType)
        {
            BattlEyeDisconnected?.Invoke(new BattlEyeDisconnectEventArgs(loginDetails, disconnectionType));
        }

        public event BattlEyeMessageEventHandler? BattlEyeMessageReceived;
        public event BattlEyeConnectEventHandler? BattlEyeConnected;
        public event BattlEyeDisconnectEventHandler? BattlEyeDisconnected;
    }

    public class StateObject
    {
        public Socket WorkSocket { get; }
        public const int BufferSize = 2048;
        public byte[] Buffer = new byte[BufferSize];
        public StringBuilder Message = new StringBuilder();
        public int PacketsTodo;

        public StateObject(Socket workSocket)
        {
            WorkSocket = workSocket;
        }
    }
}

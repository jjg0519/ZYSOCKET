﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZYSocket.ClientB;
using ZYSocket.share;
using System.Linq.Expressions;

namespace ZYSocket.RPC.Client
{
    public delegate void ClientOtherBinaryInputHandler(int cmd, ReadBytes read);

    public class RPCClient
    {
        public SocketClient Client { get; set; }

        public ZYNetRingBufferPool Stream { get; set; }

        public event ClientOtherBinaryInputHandler DataOn;

        public event ClientMessageInputHandler Disconn;

        public RPC RPC_Call { get; set; }

        #region Reg Call
        public void CallAsyn<Mode>(Expression<Action<Mode>> action)
        {
            RPC_Call.CallAsyn<Mode>(action);
        }
        public void CallAsyn<Mode, Result>(Expression<Func<Mode, Result>> action, Action<AsynReturn> Callback)
        {
            RPC_Call.CallAsyn<Mode, Result>(action, Callback);
        }

        public void CallAsyn<Mode>(Expression<Action<Mode>> action, Action<AsynReturn> Callback)
        {
            RPC_Call.CallAsyn<Mode>(action, Callback);
        }

        public Result Call<Mode, Result>(Expression<Func<Mode, Result>> action)
        {
            return RPC_Call.Call<Mode, Result>(action);
        }

        public void Call<Mode>(Expression<Action<Mode>> action)
        {
            RPC_Call.Call<Mode>(action);
        }

        #endregion

        /// <summary>
        /// 是否连接
        /// </summary>
        protected bool IsConnect { get; set; }

        public RPCClient()
        {
            RPC_Call = new RPC();
            RPC_Call.CallBufferOutSend += RPC_Call_CallBufferOutSend;
        }

        public void RegModule(object o)
        {
            RPC_Call.RegModule(o);
        }
        private void RPC_Call_CallBufferOutSend(byte[] data)
        {
            if (IsConnect)
                Client.Send(data);
                     
        }

        public bool Connection(string host, int port)
        {
            if (!IsConnect)
            {
             
                Stream = new ZYNetRingBufferPool(1024 * 1024); //1M
                Client = new SocketClient();
                Client.BinaryInput += Client_BinaryInput;
                Client.MessageInput += Client_MessageInput;

                if (Client.Connect(host, port))
                {
                    IsConnect = true;
                    Client.StartRead();
                    return true;
                }
                else
                    return false;
            }
            else
                return true;
        }

        private void Client_MessageInput(string message)
        {
            IsConnect = false;

            if (Disconn != null)
                Disconn(message);
        }



        private void Client_BinaryInput(byte[] data)
        {

            Stream.Write(data);

            byte[] datax;
            while (Stream.Read(out datax))
            {
                BinaryInput(datax);
            }
        }

        public void BinaryInput(byte[] data)
        {
            ReadBytes read = new ReadBytes(data);

            int lengt;
            int cmd;

            if (read.ReadInt32(out lengt) && read.Length == lengt && read.ReadInt32(out cmd))
            {
                switch (cmd)
                {
                    case 1001001:
                        {
                            ZYClient_Result_Return val;

                            if (read.ReadObject<ZYClient_Result_Return>(out val))
                            {
                                RPC_Call.SetReturnValue(val);
                            }
                        }
                        break;
                    case 1001000:
                        {

                            RPCCallPack tmp;

                            if (read.ReadObject<RPCCallPack>(out tmp))
                            {
                                object returnValue;
                             

                                if (RPC_Call.RunModule(tmp, out returnValue))
                                {
                                    if (tmp.IsNeedReturn)
                                    {
                                        ZYClient_Result_Return var = new ZYClient_Result_Return()
                                        {
                                            Id = tmp.Id,
                                            CallTime = tmp.CallTime,
                                            Arguments = tmp.Arguments
                                        };

                                        if (returnValue != null)
                                        {
                                            var.Return = MsgPackSerialization.GetMsgPack(returnValue.GetType()).PackSingleObject(returnValue);
                                            var.ReturnType = returnValue.GetType();
                                        }

                                        Client.Send(BufferFormat.FormatFCA(var));
                                    }
                                    
                                }
                            }
                        }
                        break;
                    default:
                        {
                            if (DataOn != null)
                                DataOn(cmd,read);
                        }
                        break;
                }


            }

        }
    }
}

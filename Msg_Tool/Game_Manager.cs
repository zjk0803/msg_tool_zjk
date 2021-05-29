/*
 * Game_Manager.cs
 *
 *  Created on: Dec 12, 2016
 *      Author: zhangyalei
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Assets._07.Network;

using System.Collections;

using UnityEngine;
using Sts.Manager;

using Sts.SaveDataManagement;

using Sts.Utils;
using DG.Tweening;

using UnityEditor;
using Sts.Model;
using SSJJ.Manager;
using thrift.sts;
using System.Threading;

namespace Msg_Tool
{
    class Game_Manager
    {
        static private Game_Manager instance_ = null;
        private Player player_ = null;
        private int robot_num_ = 0;
        private int robot_login_ = 0;
        private long send_interval_ = 0;
        private long login_interval_ = 0;
        private long run_time_ = 0;
        private string center_ip_ = "";
        private int center_port_ = 0;
        private long last_login_tick_ = 0;
        private bool robot_run_ = false;
        private bool robot_log_ = false;
        private string conf_path_ = "config/sys_conf.txt";
        private Dictionary<string, List<string>> conf_list_ = new Dictionary<string, List<string>>();
        private Dictionary<Endpoint, Player> player_map_ = new Dictionary<Endpoint, Player>();
        private List<Player> drop_list_ = new List<Player>();
        private object player_map_moni_ = new object();
        private object robot_run_moni_ = new object();
        string steamUserId = "";
        string steamName = "";
        int steamLevel = 0;
        bool steamValidateAuth = false;

        static public Game_Manager instance
        {
            get
            {
                if (instance_ == null)
                    instance_ = new Game_Manager();
                return instance_;
            }
        }
        public ServerConfig TestServer;
        private Game_Manager()
        {
            //RpcServer.Instance.TestServer = new ServerConfig("112.124.202.217", 7911, 10000);
            InitServer();
        }
        private void InitServer()
        {

            //初始化游戏服务
            TestServer = new ServerConfig("112.124.202.217", 7911, 10000);

        }




        public int robot_num
        {
            get{ return robot_num_;}
            set{ robot_num_ = value;}
        }

        public long send_interval
        {
            get
            {
                return send_interval_;
            }
            set
            {
                send_interval_ = value;
            }
        }

        public long login_interval
        {
            get
            {
                return login_interval_;
            }
            set
            {
                login_interval_ = value;
            }
        }

        public long run_time
        {
            get
            {
                return run_time_;
            }
            set
            {
                run_time_ = value;
            }
        }

        public string center_ip
        {
            get
            {
                return center_ip_;
            }
            set
            {
                center_ip_ = value;
            }
        }

        public int center_port
        {
            get
            {
                return center_port_;
            }
            set
            {
                center_port_ = value;
            }
        }

        public bool robot_log
        {
            get { return robot_log_; }
            set { robot_log_ = value; }
        }

        public void robot_login()
        {
            lock (robot_run_moni_)
            {
                if (robot_run_)
                {
                    Log.debug_log("机器人已经开始运行");
                    return;
                }
                robot_run_ = true;
                Log.debug_log("机器人开始登陆");
            }
        }

        public void robot_logout()
        {
            lock (robot_run_moni_)
            {
                if (robot_run_ == false)
                {
                    Log.debug_log("没有机器人在运行");
                    return; 
                }
                robot_run_ = false;
                robot_login_ = 0;
                Log.debug_log("机器人开始下线");
            }
            lock (player_map_moni_)
            {
                foreach (KeyValuePair<Endpoint, Player> kv in player_map_)
                {
                    if (kv.Value != player_)
                        drop_list_.Add(kv.Value);
                }
            }
        }

        public int init_conf()
        {
            try
            {
                bool clear_map = true;
                init_conf_path();

                foreach(string path in conf_list_["msg_struct_path"])
                {
                    Log.debug_log("加载消息配置,path:" + path);
                    Struct_Manager.instance.load_config(path, clear_map);
                    if (clear_map)
                        clear_map = false;
                }

                clear_map = true;
                foreach (string path in conf_list_["cmd_list_path"])
                {
                    Log.debug_log("加载命令配置,path:" + path);
                    Msg_Parse.load_cmd_list(path, clear_map);
                    if (clear_map)
                        clear_map = false;
                }

                clear_map = true;
                foreach (string path in conf_list_["error_code_path"])
                {
                    Log.debug_log("加载错误号配置,path:" + path);
                    Error_Code.load_error_code(path, clear_map);
                    if (clear_map)
                        clear_map = false;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.debug_log(ex.Message);
                return -1;
            }
        }

        private int init_conf_path(bool clear_map = true)
        {
            if (clear_map)
            {
                conf_list_.Clear();
            }
            StreamReader sr = new StreamReader(conf_path_, Encoding.Default);
            String line = null;
            while ((line = sr.ReadLine()) != null)
            {
                string[] conf_list_str = line.Split('=');
                string conf_name = conf_list_str[0].Replace(" ", "").Replace("\t", "");
                string conf_path = conf_list_str[1].Replace(" ", "").Replace("\t", "");

                string[] path_array = conf_path.Split(':');
                List<string> conf_path_list = new List<string>();
                
                foreach(string path in path_array)
                {
                    conf_path_list.Add(path);
                }
                conf_list_[conf_name] = conf_path_list;
            }
            sr.Close();
            return 0;
        }

        public bool status()
        {
            if (player_ == null || player_.end_point.connect_status == false)
                return false;
            return true;
        }

        public void init_user(string role_name)
        {
            StartTestRPC();
            //RpcServer.Instance.Client.GetAchievement(int.Parse(role_name));
            //Test1(int.Parse(role_name));
            //GetDataTest(Convert.ToInt32(role_name));
            //GetDataTest(1);
            InitServer();
            GetDataTest(1);
            //RpcServer.Instance.Client.GetAchievement(1);//1
            if (player_ != null)
            {
                player_map_.Remove(player_.end_point);
                player_ = null;
                Log.debug_log("存在该用户，可以登录");
            }
     
            else
            {
                Log.debug_log("没有该用户");
             
            };
            player_ = new Player(role_name);
            //bool respon = false;
            //GetUserInfoByRPC();
            //CallRpcRemoteThread(
            //    () =>
            //    {
            //        respon = RpcServer.Instance.SteamClient.ValidateAuthTicket(role_name, steamUserId, steamName);
            //    },
            //    () =>
            //    {
            //        steamValidateAuth = respon;
            //        if (respon)
            //        {
            //            Debug.Log("steam 用户登录验证 成功! steamId: " + steamUserId + " steamName: " + steamName);
            //            SaveDataManager.Instance.steamId = steamUserId;
            //            SaveDataManager.Instance.steamName = steamName;
            //            MultipleAPIController.Instance.steamLoginResult = true;
              

            //        }
            //        else
            //        {
            //            Debug.Log("steam 用户登录验证 失败!!");
            //        }

            //    //根据steamId结果 读取本地存档
            //    //GetFilesNumList();
            //    GetUserInfoByRPC();

            //    }
            //    , "ValidateAuthTicket"
            //    , ERpcType.Steam
            //    );

        }
        public GameObject[] fileObj;
        Dictionary<int, Sts.SaveDataManagement.UserData> userDataList = new Dictionary<int, Sts.SaveDataManagement.UserData>();
        private string testUserId = "";
        void GetUserInfoByRPC()
        {
            string id;

            steamUserId = "1";
            id = steamUserId;
            
            
            for (int i = 1; i < fileObj.Length; i++)
            {
                fileObj[i].SetActive(false);
            }
            userDataList.Clear();
            List<LoginUserData> respon = null;
            //BtnLock = false;
            CallRpcRemoteThread(
                   () =>
                   {
                       respon = RpcServer.Instance.Player_client.GetUserDataList(id);
                   },

                  () =>
                  {
                      if (respon.Count > 0)
                      {
                          for (int i = 0; i < respon.Count; i++)
                          {
                              Sts.SaveDataManagement.UserData userData = new Sts.SaveDataManagement.UserData();
                              userData.bookExp = respon[i].BookExp;
                              userData.userId = respon[i].UserId;
                              userData.startTiming = respon[i].StartTiming;
                              userData.name = respon[i].SteamName;
                              //serverTime = respon[i].ServerTime;
                              userDataList.Add(i, userData);
                          }

                          for (int i0 = 0; i0 < userDataList.Count; i0++)//为每个存档进行更新
                      {
                              if (i0 >= 3)
                                  break;
                          // if (FilesIndex.Contains(i0))
                          
                              fileObj[i0].SetActive(true);  
                          // else
                          //     ClearFile(i0);
                      }
                          //butTrs.localPosition = new Vector3(butTrs.localPosition.x, userDataList.Count == 1 ? 13 : (userDataList.Count == 2 ? -236 : -463));
                      }
                  });


        }
        public void StartTest()
        {
            List<int> useridList = new List<int>();
            for (int i = 0; i < 10; i++) {
                useridList.Add(i);
            }

            for (int i = 0; i < useridList.Count; i++)
            {
                GetDataTest(useridList[i]);
            }
        }

        public void GetDataTest(int userid)
        {


            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetAchievement(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetPlayerTalent(userid);
            }, () => { }); CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetAccelerateRemainTime(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Hero_client.GetHeroSkillData(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetMonthCardRemainTime(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Item_client.GetItemList(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Equip_client.GetEquipmentList(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Hero_client.GetHeroDataList(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetMainTaskList(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetDailyTask(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetGuideData(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetGuideTask(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetBasicPlotData(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetDailyBonus(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Guild_client.GetGuildInitInfo(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Build_Client.GetBuildingList(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetAllSupportHeroData(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetMazeInfo(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Actionpoint_client.GetActionPoint(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Story_client.GetExperienceInfo(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetLeaderSkill(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Story_client.GetHeroFavour(userid);
            }, () => { });
            CallRpcRemoteThread(() => {
                RpcServer.Instance.Crusade_client.GetCrusadeData(userid);
            }, () => { });


        }
        public void GetDataTest1(int userid)
        {
           CallRpcRemoteThread(() => {
                RpcServer.Instance.Client.GetAchievement(userid);
                //RpcServer.Instance.Client.LoginPlus(1,"1","1",1);
                }, () => {
                    
                });
        }


        public void connect(string ip, int port)
        {
            player_.end_point.connect(ip, port);
        }

        public void fini_user()
        {
            if (!status())
            {
                Log.debug_log("连接已断开");
                return;
            }
            player_.end_point.disconnect();
            player_ = null;
        }

        public void add_player(Player p)
        {
            lock (player_map_moni_)
            {
                if (!player_map_.ContainsValue(p))
                {
                    player_map_.Add(p.end_point, p);
                    Log.debug_log("账户[" + steamUserId + "]登录，已登录账户数量：" + player_map_.Count.ToString());
                }
            }
        }

        public void rmv_player(Player p)
        {
            lock (player_map_moni_)
            {
                if (player_map_.ContainsValue(p))
                {
                    player_map_.Remove(p.end_point);
                    Log.debug_log("账户[" + steamUserId + "]下线，还剩余账户数量：" + player_map_.Count.ToString());
                }
            }
        }

        public Player get_player(Endpoint ep)
        { 
            lock (player_map_moni_)
            {
                if(player_map_.ContainsKey(ep))
                    return player_map_[ep];
                return null;
            }
        }

        public void push_drop_player(Player p)
        {
            drop_list_.Add(p);
        }

        public int time_tick()
        {
            Thread_Manager.instance.wait(100);
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long tick = Convert.ToInt64(ts.TotalMilliseconds);
            lock (robot_run_moni_)
            {
                if (robot_run_ && robot_login_ < robot_num && tick - last_login_tick_ >= login_interval_)
                {
                    last_login_tick_ = tick;
                    robot_login_++;
                    Player p = new Player(robot_log_);
                    p.end_point.connect(center_ip_, center_port_);
                }
            }
            lock (player_map_moni_)
            {
                foreach (KeyValuePair<Endpoint, Player> kv in player_map_)
                {
                    kv.Value.do_tick(tick);
                }
            }
            for (int i = 0; i < drop_list_.Count; i++)
            {
                Player p = drop_list_[i];
                drop_list_.Remove(p);
                p.end_point.disconnect();
                rmv_player(p);
            }
            return 0;
        }

        public void process_buffer(Endpoint ep, Byte_Buffer buffer)
        {
            Player p = get_player(ep);
            while (buffer.readable_length() > 0)
            {
                if (p.end_point.merge_state == 0)
                {
                    uint read_len = 0;
                    if (p.end_point.len_data == null)
                    {
                        if (buffer.readable_length() < 2)
                        {
                            p.end_point.len_data = new byte[2];
                            p.end_point.len_data[0] = (byte)buffer.read_int8();
                            p.end_point.merge_state = 1;
                            return;
                        }
                        read_len = buffer.read_uint16();
                    }
                    else 
                    {
                        read_len = BitConverter.ToUInt16(p.end_point.len_data, 0);
                    }
                    uint len = (((read_len) & 0x1f) << 8) | (((read_len) & 0xff00) >> 8);
                    if (p.end_point.buffer_data == null)
                    {
                        if (buffer.readable_length() < (int)len)
                        {
                            p.end_point.buffer_data = new Byte_Buffer();
                            p.end_point.buffer_data.copy(buffer);
                            p.end_point.remain = (int)len - buffer.readable_length();
                            buffer.read_complete();
                            p.end_point.merge_state = 2;
                            return;
                        }
                        uint cmd = buffer.read_uint8();
                        Bit_Buffer buf = new Bit_Buffer(buffer.rdata(), (int)len - 1);
                        buffer.rpos += ((int)len - 1);
                        process_packet(p, cmd, buf);
                    }
                    else
                    {
                        uint cmd = p.end_point.buffer_data.read_uint8();
                        Bit_Buffer buf = new Bit_Buffer(p.end_point.buffer_data.rdata(), (int)len - 1);
                        process_packet(p, cmd, buf);
                        p.end_point.buffer_data = null;
                    }
                }
                else
                {
                    if (p.end_point.merge_state == 1)
                    {
                        p.end_point.len_data[1] = (byte)buffer.read_int8();
                        p.end_point.merge_state = 0;
                    }
                    else if (p.end_point.merge_state == 2)
                    {
                        if (buffer.readable_length() < p.end_point.remain)
                        {
                            p.end_point.buffer_data.copy(buffer);
                            p.end_point.remain -= buffer.readable_length();
                            buffer.read_complete();
                            return;
                        }
                        else
                        {
                            p.end_point.buffer_data.copy(buffer.rdata(), p.end_point.remain);
                            buffer.rpos += p.end_point.remain;
                            p.end_point.merge_state = 0;
                        }
                    }
                }
            }
        }

        public void send_to_server(uint cmd_id, Bit_Buffer buffer)
        {
            player_.end_point.send_to_server(cmd_id, buffer);
        }

        public void send_to_server(int seq)
        {
            player_.req_client_msg(seq);
        }

        private int process_packet(Player p, uint cmd, Bit_Buffer buf)
        {
            int ret = 0;
            switch (cmd)
            {
                case Msg.RES_HEARTBEAT:
                    ret = p.res_heartbeat(buf);
                    break;
                case Msg.RES_SELECT_GATE:
                    ret = p.res_select_gate(buf);
                    break;
                case Msg.RES_CONNECT_GATE:
                    ret = p.res_connect_gate(buf);
                    break;
                case Msg.RES_ROLE_LIST:
                    ret = p.res_role_list(buf);
                    break;
                case Msg.RES_ENTER_GAME:
                    ret = p.res_enter_game(buf);
                    break;
                case Msg.RES_ERROR_CODE:
                    ret = p.res_error_code(buf);
                    break;
                case Msg.RES_TEST:
                    ret = p.jiekou(buf);
                    break;
                default:
                    ret = p.res_server_msg(cmd, buf);
                    break;
            }
            return ret;
        }

        private static AutoResetEvent eventWait = new AutoResetEvent(false);
        //函数名调用锁机制
        private List<string> method_lock_list = new List<string>();
        private Queue<string> rpcMethodQueue = new Queue<string>();
        private Queue<ERpcType> rpcTypeQueue = new Queue<ERpcType>();
        private Queue<Action> rpcActionQueue = new Queue<Action>();
        private Queue<Action> callbackActionQueue = new Queue<Action>();
        private Action rpcAction;
        private Action callbackAction;
        private ERpcType serverName;
        private Thread t1;
        public RpcRespone respone = new RpcRespone();
        private int retCode = 0;

        //使用线程调用远程RPC请求
        //modify by qc 增加rpc单一锁.禁止部分消息频繁调用
        public void CallRpcRemoteThread(Action action, Action callback, string method_lock = "", ERpcType rpcType = ERpcType.Sts)
        {
 

            //安全锁 确保callbackAction  消息队列一致性
            eventWait.WaitOne();
            rpcActionQueue.Enqueue(action);
            callbackActionQueue.Enqueue(callback);
            rpcMethodQueue.Enqueue(method_lock);
            rpcTypeQueue.Enqueue(rpcType);
            eventWait.Set();
        }


        //开启测试独立通讯
        public void StartTestRPC()
        {
            t1 = new Thread(this.ConnectionServerThread);//开启独立的rpc通讯线程
            t1.Start();
            eventWait.Set();
        }

        //服务端调用线程run 
        //轮询处理消息队列
        void ConnectionServerThread()
        {
            while (true)
            {
                //进程可以通信
                if (respone.Check(RpcRespone.RpcResponStatus.Idel) && callbackAction == null && rpcActionQueue.Count > 0)
                {
                    //安全锁 确保callbackAction 
                    eventWait.WaitOne();
                    //Debug.LogError("----------------线程申请到锁----------------");
                    //发起通信
                    respone.StartRespon();

                    //出栈消息
                    rpcAction = rpcActionQueue.Dequeue();
                    callbackAction = callbackActionQueue.Dequeue();
                    serverName = rpcTypeQueue.Dequeue();
                    //Debug.Log(string.Format("出栈并执行一条消息 还有 {0} 条", rpcActionQueue.Count));

                    //调用RPC
                    CallRpcMethod(rpcAction, serverName);
                    //放行
                    //Debug.Log("释放锁 ConnectionServerThread");
                    eventWait.Set();
            }
                else if (respone.Check(RpcRespone.RpcResponStatus.Start))
                {
                    //Debug.LogError("还在通讯中！？ -1");
                }
                else if (callbackAction != null)
                {
                    //Debug.LogError("还在通讯中！？ callbackAction!=null");
                }
                else if (rpcActionQueue.Count == 0)
                {
                    //Debug.Log("消息队列空了 ");
                    //等待 原来数值500 目前改动20
                    Thread.Sleep(20);
                }
                else
                {
                    //Debug.Log("这里不应该走到吧, 不是idle 还有消息处理.现在的又处理完了 ");
                    Thread.Sleep(100);
                }
                //交出抢占
                Thread.Sleep(0);
            }

        }

        /// <summary>
        /// 最终调用RPC消息
        /// </summary>
        /// <param name="action"></param>
        /// <param name="rpcType"></param>
        void CallRpcMethod(Action action, ERpcType rpcType)
        {
            retCode = RpcServer.Instance.CallRpcRemote(rpcAction, serverName);

            if (retCode == 0)
            {
                //Debug.Log("通信成功 0 " + callbackAction.ToString());
                respone.ResponSucc();
            }
            else if (retCode > 0)
            
                //Debug.LogError("通信失败 retCode : " + retCode);
                respone.ResponFail();//网络连接失败
            }
        }






    }


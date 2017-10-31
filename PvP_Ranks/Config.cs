using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace PvP_Ranks
{
    //contains static fields for use to connect to the given redis channel.
    //##Here is where you change fields for use in the Ranks class program.
    static class Config
    {
        public static IPAddress ip = IPAddress.Parse("127.0.0.1"); //Input IP Address. Change from local to wherever the Redis channel is?
        public static int port = 7777; //default terraria port. Change if needed.
        public static IPEndPoint ipe = new IPEndPoint(ip, port); //Combination of IP and port to form end point.     //.ToString() = 127.0.0.1:7777
        public static string redisChannelName = ""; //The name of the redis channel. Edit this field to the proper channel name for the program to work.
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace PvP_Ranks
{
    //This class contains storage for participants and their associated elo.
    //Stores ONLY participants. Players not stored are assumed to have an elo of the starting elo value.
    //Simply create a Ranks object to begin using this code then call Ranks.Subscribe() and it will begin storing changes. 
    //Contact Tsohg (Alias on Discord) for any questions on the original structure of the program.
    public class Ranks
    {
        private const int STARTING_ELO = 1000; //A starting elo value constant for all new participants.

        private static Dictionary<int, int> uuidEloDict = new Dictionary<int, int>(); //Contains the player's UUID associated with their ELO value.

        public void Subscribe() //Not sure if it requires a loop?
        {
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(Config.ipe.ToString()); //returns IP and port in form:   127.0.0.1:7777    IP:PORT
            ISubscriber sub = redis.GetSubscriber();
            sub.SubscribeAsync(Config.redisChannelName, (channel, message) => { ParseDataAndApplyChange(channel, message); });
        }

        //##This code will need to be changed for use in the DG server's redis. Edit the Config class to change things such as the IP address endpoint and the Redis channel name.
        public void PublishDictionary(Dictionary<int, int> dict) //Publishes a dictionary sorted in descending order.
        {
            var sortedDict = SortDictionary(dict);

            //##This code may need to be changed to suit the DG server.
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(Config.ipe.ToString()); //For example, passes     127.0.0.1:7777    into the .Connect() method.
            ISubscriber pub = redis.GetSubscriber();
            pub.Publish(Config.redisChannelName, ConvertDictToJSON(sortedDict)); //Method converts the sorted dictionary into a JSON string.
        }

        //Sorts a given dictionary.
        private Dictionary<int, int> SortDictionary(Dictionary<int, int> toBeSorted)
        {
            var sortedDict = new Dictionary<int, int>();

            foreach (var entry in toBeSorted.OrderByDescending(x => x.Value))
                sortedDict.Add(entry.Key, entry.Value);

            return sortedDict;
        }

        //converts dictionary message into a JSON string.
        //##Should work to convert the dictionary object into a suitable format for JSON. If not, Contact me.
        private string ConvertDictToJSON(Dictionary<int, int> dict)
        {
            string jsonMessage;

            jsonMessage = JsonConvert.SerializeObject(dict);

            return jsonMessage;
        }

        //parses data from input and applies the necessary elo changes based on the round result.
        //##After each call of this, it will publish a sorted uuid:elo dictionary. It is called in the Subscribe() method.
        private void ParseDataAndApplyChange(RedisChannel channel, RedisValue message)
        {
            //**Accepts a string message which should be in JSON format
            //The format for the JSON payload is below.

            /* {
             *  "Winner":0,
             *  "Loser":0
             *  }
             */

            //Where the value after winner and loser are the uuids.

            Round_Result rR = JsonConvert.DeserializeObject<Round_Result>(message); //deserializes the RedisValue input (JSON) into a Match_Results object.

            //check to see if both participants are inside the dictionary. If not, add them with the default elo value.
            if (!uuidEloDict.ContainsKey(rR.winner))
                uuidEloDict.Add(rR.winner, STARTING_ELO);

            if (!uuidEloDict.ContainsKey(rR.loser))
                uuidEloDict.Add(rR.loser, STARTING_ELO);

            int changeAmount = CalculateChangeAmount(rR.winner, rR.loser); //calculates the change amount based on the match.

            UpdateElo(rR.winner, rR.loser, changeAmount); //updates the uuidEloDict with new elo values.

            PublishDictionary(uuidEloDict); //after change is completed, publishes a new version of the uuid elo dictionary.
        }

        //Calculates the amount of change needed for each player's elo value. Depending on how the loser fares, the value returned will be smaller if the loser did exceptionally well in the duel.
        //If the loser did very poorly in the duel, the value returned will be larger.
        //The formula may be changed here to change how the elo gain functions. But any formula must result in a POSITIVE integer only. {X | X >= 0}

        //##The formula may need to be changed depending on what suits the needs of the community. I have given a very basic 'starter' formula.
        //##If there are any problems in how elo is given/taken, the problem is right here in CalculateChangeAmount.
        private int CalculateChangeAmount(int winnerUUID, int loserUUID)
        {
            int eloChangeValue = 0; //0 is a safe amount. Does not change the elo value of a player if added.

            //Formula:
            // |(Winner's Elo - Loser's Elo)| = eloDistance
            // |15 - eloDistance / 10|

            //Domain of eloChangeValue = 1 <= X <= 15
            //Per Round the cap on elo is 15.

            //If the formula is a negative integer, it will return 5. if the formula returns a positive integer, it will return the value.
            //This ensures that if a high elo player wins against a low elo player, the elo rewarded will be 5.
            //This also ensures that if a high elo player loses against a low elo player, the elo rewarded will be the large value generated.

            int winnerElo = uuidEloDict[winnerUUID]; //Fetches the ELO from the dictionary based on the uuid of the player.
            int loserElo = uuidEloDict[loserUUID];

            int eloDistance = winnerElo - loserElo; //From formula.

            eloChangeValue = Math.Abs(15 - (Math.Abs(eloDistance / 10))); //Formula use. 15 and 10 are constants to get the proper value. Domain of formula listed above.

            if (eloChangeValue > 15 && winnerElo > loserElo) //Return value if a significantly higher elo wins against a significantly lower elo.
                return 1; //returns 1 if higher elo.
            else return eloChangeValue; //a Catch-All. If there is any errors, it will simply return 0 as the change value.
        }

        //Updates a player's elo by adding the change amount to the elo value. Negative integers lower the elo value and Positive integers increase the elo value.
        private void UpdateElo(int winnerUUID, int loserUUID, int changeAmount)
        {
            //Change the values of the given 2 players' ELO based on uuid and change amount.
            uuidEloDict[winnerUUID] += changeAmount;
            uuidEloDict[loserUUID] -= changeAmount;
        }
    }
}

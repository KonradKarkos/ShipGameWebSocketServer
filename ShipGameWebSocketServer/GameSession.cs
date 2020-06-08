using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShipGameWebSocketServer
{
    public enum CoordinateState
    {
        Water,
        Ship,
        HitWater,
        HitShip,
        FatalHitShip
    }
    public enum GameState
    {
        Waiting,
        InProgress,
        Resolved,
        Lost,
        Won
    }
    class GameSession
    {
        //Key - Ship size, Key - Ship directon multiplier, Value - Starting coordinate
        public Dictionary<int, KeyValuePair<int, int>> PlayerOneShips { get; set; }
        public Dictionary<int, KeyValuePair<int, int>> PlayerTwoShips { get; set; }
        public GameState GameState { get; set; }
        public bool PlayerOneTurn { get; set; }
        public bool PlayerOneReady { get; set; }
        public bool PlayerTwoReady { get; set; }
        public string PlayerOneSessionID { get; set; }
        public string PlayerTwoSessionID { get; set; }
        public byte[] Boards { get; set; }
        public bool AttackCoordinates(int player, int letter, int number)
        {
            bool attacked = false;
            int attackCoordinate = player * 100 + letter + number * 10;
            if ((CoordinateState)Boards[attackCoordinate] == CoordinateState.Water)
            {
                Boards[attackCoordinate] = (byte)CoordinateState.HitWater;
                attacked = true;
            }
            else if((CoordinateState)Boards[attackCoordinate] == CoordinateState.Ship)
            {
                Boards[attackCoordinate] = (byte)CoordinateState.HitShip;
                int hits = 0;
                if (player == 0)
                {
                    foreach (var ship in PlayerOneShips)
                    {
                        for(int i=0;i<ship.Key;i++)
                        {
                            if((CoordinateState)Boards[ship.Value.Value + i * ship.Value.Key] == CoordinateState.HitShip)
                            {
                                hits++;
                            }
                        }
                        if(hits == ship.Key)
                        {
                            for(int i=0;i<ship.Key;i++)
                            {
                                Boards[ship.Value.Value + i * ship.Value.Key] = (byte)CoordinateState.FatalHitShip;
                            }
                        }
                        hits = 0;
                    }
                }
                else
                {
                    foreach (var ship in PlayerTwoShips)
                    {
                        for (int i = 0; i < ship.Key; i++)
                        {
                            if ((CoordinateState)Boards[100 + ship.Value.Value + i * ship.Value.Key] == CoordinateState.HitShip)
                            {
                                hits++;
                            }
                        }
                        if (hits == ship.Key)
                        {
                            for (int i = 0; i < ship.Key; i++)
                            {
                                Boards[100 + ship.Value.Value + i * ship.Value.Key] = (byte)CoordinateState.FatalHitShip;
                            }
                        }
                        hits = 0;
                    }
                }
                attacked = true;
            }
            return attacked;
        }
        public GameState GetRefreshedGameState()
        {
            if(!new ArraySegment<byte>(Boards,0,100).Where(b => b.Equals((byte)CoordinateState.Ship)).Any())
            {
                this.GameState = GameState.Lost;
            }
            else if(!new ArraySegment<byte>(Boards, 100, 100).Where(b => b.Equals((byte)CoordinateState.Ship)).Any())
            {
                this.GameState = GameState.Won;
            }
            return this.GameState;
        }
        public bool TryAddPlayer(string PlayerSessionID)
        {
            if(PlayerOneSessionID.Length == 0)
            {
                PlayerOneSessionID = PlayerSessionID;
                if(PlayerTwoSessionID.Length > 0 && GameState == GameState.Waiting)
                {
                    PlayerOneTurn = true;
                }
                return true;
            }
            else if(PlayerTwoSessionID.Length == 0)
            {
                PlayerTwoSessionID = PlayerSessionID;
                if (PlayerOneSessionID.Length > 0 && GameState == GameState.Waiting)
                {
                    PlayerOneTurn = true;
                }
                return true;
            }
            return false;
        }
        public GameSession()
        {
            Boards = new byte[200];
            PlayerOneSessionID = "";
            PlayerTwoSessionID = "";
            PlayerOneReady = false;
            PlayerTwoReady = false;
            PlayerOneTurn = false;
            PlayerOneShips = new Dictionary<int, KeyValuePair<int, int>>();
            PlayerTwoShips = new Dictionary<int, KeyValuePair<int, int>>();
        }
        public GameSession(string PlayerSessionID)
        {
            this.PlayerOneSessionID = PlayerSessionID;
            PlayerTwoSessionID = "";
            this.GameState = GameState.Waiting;
            Boards = new byte[200];
            PlayerOneReady = false;
            PlayerTwoReady = false;
            PlayerOneTurn = false;
            PlayerOneShips = new Dictionary<int, KeyValuePair<int, int>>();
            PlayerTwoShips = new Dictionary<int, KeyValuePair<int, int>>();
        }
        public GameSession(string PlayerOneSessionID, string PlayerTwoSessionID)
        {
            this.PlayerOneSessionID = PlayerOneSessionID;
            this.PlayerTwoSessionID = PlayerTwoSessionID;
            this.GameState = GameState.Waiting;
            Boards = new byte[200];
            PlayerOneReady = false;
            PlayerTwoReady = false;
            PlayerOneTurn = false;
            PlayerOneShips = new Dictionary<int, KeyValuePair<int, int>>();
            PlayerTwoShips = new Dictionary<int, KeyValuePair<int, int>>();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

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
        public Dictionary<int, KeyValuePair<int, int>>[] PlayersShips { get; set; }
        public GameState GameState { get; set; }
        public bool[] PlayersReady { get; set; }
        public bool[] PlayerTurns { get; set; }
        public string[] PlayerSessionIDs { get; set; }
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
                foreach (var ship in PlayersShips[player])
                {
                    for (int i = 0; i < ship.Key; i++)
                    {
                        if ((CoordinateState)Boards[ship.Value.Value + i * ship.Value.Key] == CoordinateState.HitShip)
                        {
                            hits++;
                        }
                    }
                    if (hits == ship.Key)
                    {
                        for (int i = 0; i < ship.Key; i++)
                        {
                            Boards[ship.Value.Value + i * ship.Value.Key] = (byte)CoordinateState.FatalHitShip;
                        }
                    }
                    hits = 0;
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
        public bool TryAddPlayer(string playerSessionID)
        {
            for(int i=0; i< PlayerSessionIDs.Length; i++)
            {
                if(PlayerSessionIDs[i].Length == 0)
                {
                    PlayerSessionIDs[i] = playerSessionID;
                    return true;
                }
            }
            return false;
        }
        public GameSession()
        {
            Boards = new byte[200];
            PlayerSessionIDs = new string[2];
            PlayersReady = new bool[2];
            PlayersShips = new Dictionary<int, KeyValuePair<int, int>>[2];
            PlayerTurns = new bool[2];
            for(int i=0;i<2;i++)
            {
                PlayerSessionIDs[i] = "";
                PlayersReady[i] = false;
                PlayersShips[i] = new Dictionary<int, KeyValuePair<int, int>>();
            }
            PlayerTurns[0] = true;
            PlayerTurns[1] = false;
        }
        public GameSession(string playerSessionID)
        {
            PlayerSessionIDs = new string[2];
            PlayersReady = new bool[2];
            PlayersShips = new Dictionary<int, KeyValuePair<int, int>>[2];
            PlayerTurns = new bool[2];
            PlayerSessionIDs[0] = playerSessionID;
            PlayerSessionIDs[1] = "";
            this.GameState = GameState.Waiting;
            Boards = new byte[200];
            for (int i = 0; i < 2; i++)
            {
                PlayersReady[i] = false;
                PlayersShips[i] = new Dictionary<int, KeyValuePair<int, int>>();
            }
            PlayerTurns[0] = true;
            PlayerTurns[1] = false;
        }
        public GameSession(string playerOneSessionID, string playerTwoSessionID)
        {
            PlayerSessionIDs = new string[2];
            PlayersReady = new bool[2];
            PlayersShips = new Dictionary<int, KeyValuePair<int, int>>[2];
            PlayerTurns = new bool[2];
            PlayerSessionIDs[0] = playerOneSessionID;
            PlayerSessionIDs[1] = playerTwoSessionID;
            this.GameState = GameState.Waiting;
            Boards = new byte[200];
            for (int i = 0; i < 2; i++)
            {
                PlayersReady[i] = false;
                PlayersShips[i] = new Dictionary<int, KeyValuePair<int, int>>();
            }
            PlayerTurns[0] = true;
            PlayerTurns[1] = false;
        }
    }
}

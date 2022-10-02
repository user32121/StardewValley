using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNav
{
    internal class Config
    {
        public bool enabled = true;
        public KeybindList toggleNavOverlay = new KeybindList(StardewModdingAPI.SButton.I);

        public KeybindList up = new KeybindList(StardewModdingAPI.SButton.W);
        public KeybindList down = new KeybindList(StardewModdingAPI.SButton.X);
        public KeybindList left = new KeybindList(StardewModdingAPI.SButton.A);
        public KeybindList right = new KeybindList(StardewModdingAPI.SButton.D);
        public KeybindList upRight = new KeybindList(StardewModdingAPI.SButton.E);
        public KeybindList upLeft = new KeybindList(StardewModdingAPI.SButton.Q);
        public KeybindList downRight = new KeybindList(StardewModdingAPI.SButton.C);
        public KeybindList downLeft = new KeybindList(StardewModdingAPI.SButton.Z);
        public KeybindList center = new KeybindList(StardewModdingAPI.SButton.S);

        public float displayDistanceFromCenter = 200;

        public bool allowChainDirections = true;  //note: only supported for warps
        public float chainDirectionConfirmTime = 0.5f;

        public double apiMaxBFSTime = 5;
        public double apiMaxPathingTime = 1;

        public enum DIRECTION
        {
            NONE = 0,
            LEFT,
            RIGHT,
            UP,
            DOWN,
            UPLEFT,
            UPRIGHT,
            DOWNLEFT,
            DOWNRIGHT,
            CENTER,
        }

        public Dictionary<string, Dictionary<DIRECTION, string>> warpLists = new Dictionary<string, Dictionary<DIRECTION, string>>()
        {
            { "FarmHouse", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Farm" },
            } },
            { "Barn", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Farm" },
            } },
            { "Coop", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Farm" },
            } },
            { "FarmCave", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Farm" },
            } },
            { "Farm", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.RIGHT, "BusStop" },
                { DIRECTION.DOWN, "Forest" },
                { DIRECTION.UP, "Backwoods" },
                { DIRECTION.UPLEFT, "FarmCave" },
                { DIRECTION.UPRIGHT, "68,16;68,15|Mail" },
                { DIRECTION.CENTER, "64,14|FarmHouse" },
            } },
            { "BusStop", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.RIGHT, "Town" },
                { DIRECTION.LEFT, "Farm" },
                { DIRECTION.UP, "4,3;5,3;6,3|Minecart" },
                { DIRECTION.UPLEFT, "Backwoods" },
                { DIRECTION.CENTER, "7,11|Bus" },
            } },
            { "Town", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.LEFT, "BusStop" },
                { DIRECTION.DOWN, "Beach" },
                { DIRECTION.UP, "Mountain" },
                { DIRECTION.DOWNLEFT, "Forest" },
                { DIRECTION.RIGHT, "105,79;106,79;106,79|Minecart" },
            } },
            { "Backwoods", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.UP, "Mountain" },
                { DIRECTION.DOWN, "Farm" },
                { DIRECTION.RIGHT, "BusStop" },
                { DIRECTION.LEFT, "Tunnel" },
            } },
            { "Forest", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.RIGHT, "Town" },
                { DIRECTION.UP, "Farm" },
                { DIRECTION.UPLEFT, "Woods" },
                { DIRECTION.LEFT, "5,26|Wizard" },
            } },
            { "Woods", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.RIGHT, "Forest" },
            } },
            { "Beach", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.UP, "Town" },
            } },
            { "Mountain", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Town" },
                { DIRECTION.LEFT, "Backwoods" },
                { DIRECTION.UP, "Mine" },
                { DIRECTION.UPLEFT, "Railroad" },
                { DIRECTION.CENTER, "Tent" },
            } },
            { "Railroad", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Mountain" },
                { DIRECTION.UP, "Summit" },
                { DIRECTION.UPRIGHT, "WitchWarpCave" },
            } },
            { "Tunnel", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.RIGHT, "Backwoods" },
            } },
            { "Mine", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.LEFT, "11,10;12,10|Minecart" },
                { DIRECTION.DOWN, "Mountain" },
                { DIRECTION.RIGHT, "23,9|Ladder" },
            } },
            { "Tent", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Mountain" },
            } },
            { "Greenhouse", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Farm" },
            } },
            { "Deluxe Barn", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Farm" },
            } },
            { "Deluxe Coop", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Farm" },
            } },
            { "CommunityCenter", new Dictionary<DIRECTION, string>()
            {
                { DIRECTION.DOWN, "Town" },
            } },
        };
    }
}

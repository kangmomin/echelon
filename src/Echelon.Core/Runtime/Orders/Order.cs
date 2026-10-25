using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public enum OrderType
    {
        MOVE,
        FAST_MOVE,
        ATTACK_MOVE,
        SEIZE,
        DEFEND,
        WITHDRAW,
        RESUPPLY,
        MOUNT,
        DISMOUNT,
        SET_FORMATION,
        SET_SOP,
        CALL_FIRE,
        DETACH,
        REJOIN,
        REORGANIZE,
        CANCEL,
        FOLLOW,
        HALT,
        LAY_MINES,
        CLEAR_MINES,
        FORTIFY,
        AIRDROP
    }

    public enum OrderState
    {
        QUEUED,
        PROPAGATING,
        RECEIVED,
        EXECUTING,
        DONE,
        CANCELLED
    }

    public sealed class Order
    {
        public static readonly string[] TypeNames =
        {
            "MOVE", "FAST_MOVE", "ATTACK_MOVE", "SEIZE", "DEFEND", "WITHDRAW", "RESUPPLY", "MOUNT", "DISMOUNT",
            "SET_FORMATION", "SET_SOP", "CALL_FIRE", "DETACH", "REJOIN", "REORGANIZE", "CANCEL", "FOLLOW", "HALT",
            "LAY_MINES", "CLEAR_MINES", "FORTIFY", "AIRDROP"
        };

        public static readonly string[] StateNames =
        {
            "QUEUED", "PROPAGATING", "RECEIVED", "EXECUTING", "DONE", "CANCELLED"
        };

        public static readonly OrderType[] MovementTypes =
        {
            OrderType.MOVE, OrderType.FAST_MOVE, OrderType.ATTACK_MOVE, OrderType.SEIZE, OrderType.DEFEND,
            OrderType.WITHDRAW, OrderType.LAY_MINES, OrderType.CLEAR_MINES, OrderType.FORTIFY, OrderType.AIRDROP
        };

        public static readonly OrderType[] EngineerTypes =
        {
            OrderType.LAY_MINES, OrderType.CLEAR_MINES, OrderType.FORTIFY
        };

        public int Id { get; set; }
        public long TickIssued { get; set; }
        public string Issuer { get; set; } = "player";
        public int IssuerUnitId { get; set; } = -1;
        public int TargetUnitId { get; set; } = -1;
        public OrderType Type { get; set; } = OrderType.MOVE;
        public JObject Params { get; set; } = new JObject();
        public List<OrderHop> Hops { get; set; } = new List<OrderHop>();
        public int HopIndex { get; set; }
        public OrderState State { get; set; } = OrderState.QUEUED;
        public int ParentOrderId { get; set; } = -1;
        public double Cost { get; set; }
        public long BlockedSinceTick { get; set; } = -1;
        public bool Bypass { get; set; }
        public long ReceivedTick { get; set; } = -1;

        public static Order Make(OrderType type, int target, JObject? parameters = null)
        {
            return new Order
            {
                Type = type,
                TargetUnitId = target,
                Params = parameters ?? new JObject()
            };
        }

        public string TypeName()
        {
            return TypeNames[(int)Type];
        }

        public string StateName()
        {
            return StateNames[(int)State];
        }

        public bool IsMovement()
        {
            return Array.IndexOf(MovementTypes, Type) >= 0;
        }

        public bool IsEngineer()
        {
            return Array.IndexOf(EngineerTypes, Type) >= 0;
        }

        public bool IsFinished()
        {
            return State == OrderState.DONE || State == OrderState.CANCELLED;
        }

        public bool HasPos()
        {
            return Params.Property("pos") != null;
        }

        public SimVec2 GetPos()
        {
            JToken? token = Params["pos"];
            if (token is JValue value && value.Value is SimVec2 vector)
            {
                return vector;
            }

            if (token == null)
            {
                return SimVec2.Zero;
            }

            if (token is JArray array)
            {
                return new SimVec2(array[0].Value<double>(), array[1].Value<double>());
            }

            throw new FormatException("Order 'pos' must be a SimVec2 or a two-number array.");
        }

        public void SetPos(SimVec2 position)
        {
            Params["pos"] = new JArray(position.X, position.Y);
        }

        public double GetFacing()
        {
            JToken? token = Params["facing"];
            return token == null ? Unit.NoFacing : token.Value<double>();
        }

        public void SetFacing(double heading)
        {
            Params["facing"] = heading;
        }

        public string GetString(string key, string defaultValue = "")
        {
            JToken? token = Params[key];
            if (token == null)
            {
                return defaultValue;
            }

            return token.Type == JTokenType.String
                ? token.Value<string>() ?? ""
                : token.ToString(Newtonsoft.Json.Formatting.None);
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            JToken? token = Params[key];
            if (token == null)
            {
                return defaultValue;
            }

            return token.Type == JTokenType.Boolean && token.Value<bool>();
        }

        public long EtaTick()
        {
            return Hops.Count == 0 ? TickIssued : Hops[Hops.Count - 1].ArriveTick;
        }

        public JObject ToJObject()
        {
            return new JObject
            {
                ["id"] = Id,
                ["tick"] = TickIssued,
                ["issuer"] = Issuer,
                ["issuer_unit"] = IssuerUnitId,
                ["target"] = TargetUnitId,
                ["type"] = TypeName(),
                ["params"] = Params.DeepClone(),
                ["bypass"] = Bypass
            };
        }

        public static Order FromJObject(JObject data)
        {
            Order order = new Order
            {
                Id = ReadInt(data["id"], 0),
                TickIssued = ReadLong(data["tick"], 0),
                Issuer = data["issuer"]?.Value<string>() ?? "player",
                IssuerUnitId = ReadInt(data["issuer_unit"], -1),
                TargetUnitId = ReadInt(data["target"], -1),
                Bypass = data["bypass"]?.Type == JTokenType.Boolean && data["bypass"]!.Value<bool>()
            };

            string typeName = data["type"]?.Value<string>() ?? "MOVE";
            int typeIndex = Array.IndexOf(TypeNames, typeName);
            order.Type = (OrderType)Math.Max(0, typeIndex);
            order.Params = data["params"] as JObject ?? new JObject();
            return order;
        }

        private static int ReadInt(JToken? token, int defaultValue)
        {
            return DefReader.IsNumber(token) ? (int)token!.Value<double>() : defaultValue;
        }

        private static long ReadLong(JToken? token, long defaultValue)
        {
            return DefReader.IsNumber(token) ? (long)token!.Value<double>() : defaultValue;
        }
    }
}

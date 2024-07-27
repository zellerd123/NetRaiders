using System.Collections.Generic;

namespace NetRaiders {
    public class BitPickup {
        public int id;
        public float x;
        public float y;
    }
    public class NetraiderSnapshot
    {
        public int local_player_id;
        public int server_tick;
        public int tick_rate;
        public int target_transmission;
        public int game_over_winner;
        public List<NetraiderPlayer> player_deltas;
        public List<BitPickup> spawn_pickups;
        public List<int> despawn_players;
        public List<int> despawn_pickups;
        public bool at_wap;
    }
    public class NetraiderPlayer
    {
        public int user_id;
        public string username;
        public int tick;
        public float tick_rtt;
        public float x;
        public float y;
        public float scale;
        public int untransmitted;
        public int transmitted;
    }
    public struct NetraiderInput
    {
        public float expected_tick;
        public float x;
        public float y;
    }
}

using AL.Core.Interfaces;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalWarzoneCreditService : IWarzoneCreditService
    {
        private int _credits = 0;

        public int GetCredits() => _credits;

        public void AddCredits(int amount)
        {
            _credits += amount;
            Debug.Log($"Added {amount} Warzone Credits. Total: {_credits}");
        }

        public bool SpendCredits(int amount)
        {
            if (_credits >= amount)
            {
                _credits -= amount;
                return true;
            }
            return false;
        }
    }
}

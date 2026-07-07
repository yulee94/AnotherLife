using UnityEngine;
using AL.Core;

namespace AL.Core.Interfaces
{
    public interface IChampionControlService
    {
        void Move(Vector2 direction);
        void UseSkill(int skillIndex);
        void SetAutoMode(AutoMode mode);
    }
}

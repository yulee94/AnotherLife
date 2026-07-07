using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Class", menuName = "AL/Data/Class")]
    public class ClassDefinition : ScriptableObject
    {
        public ClassFamily Family;
        public SubclassId Subclass;
        public string ClassName;
        public Sprite Icon;
    }
}

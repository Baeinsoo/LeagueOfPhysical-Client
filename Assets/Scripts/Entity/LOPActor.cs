using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData)
            where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
        }
    }
}

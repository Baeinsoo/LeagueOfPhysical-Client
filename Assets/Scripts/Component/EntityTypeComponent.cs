
namespace LOP
{
    public class EntityTypeComponent : LOPComponent
    {
        public EntityType entityType { get; private set; }

        public void Initialize(EntityType entityType)
        {
            this.entityType = entityType;
        }
    }
}

namespace Echelon.Core
{
    public class SimSystem
    {
        public virtual string GetSystemName()
        {
            return "system";
        }

        public virtual void Step(SimWorld world)
        {
        }

        public virtual long StateHash()
        {
            return 0;
        }

        public virtual bool OmitFromHash()
        {
            return false;
        }
    }
}

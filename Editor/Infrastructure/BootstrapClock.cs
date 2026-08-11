using UnityEditor;

namespace Deucarian.Bootstrap.Editor
{
    internal interface IBootstrapClock
    {
        double Now { get; }
    }

    internal sealed class UnityBootstrapClock : IBootstrapClock
    {
        public double Now => EditorApplication.timeSinceStartup;
    }
}

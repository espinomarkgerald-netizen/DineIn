using System.Collections.Generic;
using UnityEngine;

public sealed class MultiplayerWorldLifetime : MonoBehaviour
{
    private readonly List<Component> values = new();
    public void Track(Component component) { if (!values.Contains(component)) values.Add(component); }
    private void OnDestroy() { foreach (var value in values) MultiplayerWorldRegistry.Forget(value); }
}

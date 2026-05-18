using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Testers
{
    public class SimulatorEditorOnlySpawner : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] private bool useUI;
        [SerializeField] private GameObject simulatorPrefab;

        private void Awake()
        {
            if (!Application.isPlaying || simulatorPrefab == null) return;
            var go = Instantiate(simulatorPrefab);
            if (!useUI && go.TryGetComponent<XRDeviceSimulator>(out var sim))
            {
                sim.deviceSimulatorUI.SetActive(false);
            }
        }
#endif
    }
}
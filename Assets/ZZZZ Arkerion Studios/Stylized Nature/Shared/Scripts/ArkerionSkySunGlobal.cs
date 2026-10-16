using UnityEngine;

namespace Arkerion
{
    [ExecuteAlways]
    public class ArkerionSkySunGlobal : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private Material skyMaterial;
        [SerializeField] private string sunDirProperty = "_SunDirection";

        private void OnEnable() => UpdateSun();
        private void Update() => UpdateSun();

        private void UpdateSun()
        {
            if (skyMaterial == null) return;

            if (sun == null) sun = RenderSettings.sun;
            if (sun == null) return;

            Vector3 dirToSun = sun.transform.forward;
            skyMaterial.SetVector(sunDirProperty, dirToSun);
        }
    }
}

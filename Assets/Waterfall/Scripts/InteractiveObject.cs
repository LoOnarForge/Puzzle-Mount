using UnityEngine;

namespace WaterShaders
{
    public class InteractiveObject : MonoBehaviour
    {
        void Update()
        {
            Shader.SetGlobalVector("_ObjectPosition",
                new Vector4(this.transform.position.x, this.transform.position.y, this.transform.position.z, this.transform.localScale.x));
        }
    }

}


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComplexMeshMarker : MonoBehaviour
{
    private readonly List<Renderer> cachedRenderers = new List<Renderer>();
    private CameraFollow cameraFollow;
    private Coroutine registerCoroutine;

    private void OnEnable()
    {
        registerCoroutine = StartCoroutine(RegisterAfterHierarchyReady());
    }

    private void OnDisable()
    {
        if (registerCoroutine != null)
        {
            StopCoroutine(registerCoroutine);
            registerCoroutine = null;
        }

        if (cameraFollow != null)
            cameraFollow.UnregisterComplexMesh(this);

        cameraFollow = null;
    }

    private IEnumerator RegisterAfterHierarchyReady()
    {
        yield return null;

        if (!isActiveAndEnabled)
            yield break;

        registerCoroutine = null;
        RebuildAndRegister();
    }

    private void RebuildAndRegister()
    {
        if (cameraFollow == null)
            cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
        if (cameraFollow == null)
            return;

        cachedRenderers.Clear();
        GetComponentsInChildren(true, cachedRenderers);

        cameraFollow.RegisterComplexMesh(this, cachedRenderers);
    }
}
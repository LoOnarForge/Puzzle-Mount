using UnityEngine;

public class ZFightingRandomizer : MonoBehaviour
{
    [SerializeField] private Transform[] meshTransforms;
    [Tooltip("Last digit of grid X times this value (e.g. digit 3 and 0.0001 → 0.0003). Change to test how small the offset can be.")]
    [SerializeField] private float digitUnit = 0.0001f;

    private Vector3[] restingLocalScales;
    private bool hasCachedRestingScales;

    private void Awake()
    {
        ApplyPositionBasedScale();
    }

    // Recomputes uniform scale from parent grid X/Y and applies it to listed mesh transforms.
    public void ApplyPositionBasedScale()
    {
        if (meshTransforms == null || meshTransforms.Length == 0)
            return;

        EnsureRestingScalesCached();

        float scaleMultiplier = GetScaleMultiplierFromPosition(transform.position);
        for (int i = 0; i < meshTransforms.Length; i++)
        {
            Transform meshTransform = meshTransforms[i];
            if (meshTransform == null)
                continue;

            meshTransform.localScale = restingLocalScales[i] * scaleMultiplier;
        }
    }

    private void EnsureRestingScalesCached()
    {
        if (hasCachedRestingScales && restingLocalScales != null && restingLocalScales.Length == meshTransforms.Length)
            return;

        restingLocalScales = new Vector3[meshTransforms.Length];
        for (int i = 0; i < meshTransforms.Length; i++)
        {
            Transform meshTransform = meshTransforms[i];
            restingLocalScales[i] = meshTransform != null ? meshTransform.localScale : Vector3.one;
        }

        hasCachedRestingScales = true;
    }

    private float GetScaleMultiplierFromPosition(Vector3 worldPosition)
    {
        int gridX = Mathf.RoundToInt(worldPosition.x);
        int gridY = Mathf.RoundToInt(worldPosition.y);

        int lastDigitX = LastDecimalDigit(gridX);
        float magnitude = lastDigitX * digitUnit;

        bool yIsOdd = (gridY & 1) != 0;
        float signedDelta = yIsOdd ? -magnitude : magnitude;

        return 1f + signedDelta;
    }

    private static int LastDecimalDigit(int value)
    {
        int digit = value % 10;
        if (digit < 0)
            digit += 10;
        return digit;
    }
}

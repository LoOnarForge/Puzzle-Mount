using System.Collections.Generic;
using UnityEngine;

public class PowerSource : MonoBehaviour
{
    [Header("POWER SOURCE:")]
    [SerializeField] private int colorIndex;
    [SerializeField] private int maxMW = 10;

    [Space(20)]
    [Header("STATE:")]
    [SerializeField] private int availableMW;
    [SerializeField] private List<RunodeLine> circuitMembers = new List<RunodeLine>();

    [Space(20)]
    [Header("POWER SOURCE PORTS:")]
    [SerializeField] private PowerSourcePort upPort;
    [SerializeField] private PowerSourcePort rightPort;
    [SerializeField] private PowerSourcePort downPort;
    [SerializeField] private PowerSourcePort leftPort;

    [Space(20)]
    [Header("VISUAL ELEMENTS:")]
    [SerializeField] private List<Renderer> colorElements = new List<Renderer>();

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock propertyBlock;
    private Color circuitColor;

    private void Awake()
    {
        availableMW = maxMW;
        circuitColor = ColorManager.Instance.GetColor(colorIndex);
    }

 
    public void PowerRunodeLine(RunodeLine line)
    {
        if (!TakeMW())
            return;

        line.PowerUp(this, circuitColor, 1);
        AddCircuitMember(line);
    }

    public void ReturnMW()
    {
        if (availableMW < maxMW)
            availableMW++;
    }
    public bool TakeMW()
    {
        if (availableMW <= 0)
            return false;

        availableMW--;
        return true;
    }


    public void AddCircuitMember(RunodeLine line)
    {
        if (line == null || circuitMembers.Contains(line))
            return;

        circuitMembers.Add(line);
    }
    public void RemoveCircuitMember(RunodeLine line)
    {
        if (line == null)
            return;

        circuitMembers.Remove(line);
    }


    private void SetStartingColorsOnPSObject()
    {
        ColorManager colorManager = FindAnyObjectByType<ColorManager>();
        if (colorManager == null || colorManager.colors.Count == 0)
            return;

        int selectedColorIndex = Mathf.Clamp(colorIndex, 0, colorManager.colors.Count - 1);
        Color selectedColor = colorManager.GetColor(selectedColorIndex);

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer colorElement in colorElements)
        {
            if (colorElement == null)
                continue;

            colorElement.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorProperty, selectedColor);
            propertyBlock.SetColor(EmissionColorProperty, selectedColor * 2f);
            colorElement.SetPropertyBlock(propertyBlock);

            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
                return;

            spriteRenderer.color = selectedColor;
        }
    }
    private void OnValidate()
    {
        SetStartingColorsOnPSObject();
    }
}

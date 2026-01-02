using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class SelectionCard : MonoBehaviour, IPointerClickHandler
{
    private GameObject unitPrefab;
    private SelectionUI selectionUI;
    private bool isSelected;
    
    private Image icon;
    private Image overlay;
    private TMP_Text costText;

    public GameObject UnitPrefab => unitPrefab;
    public Sprite IconSprite => icon != null ? icon.sprite : null;
    public string CostValue => costText != null ? costText.text : "";

    public void Initialize(GameObject prefab, Image iconRef, TMP_Text costRef, SelectionUI ui)
    {
        this.unitPrefab = prefab;
        this.icon = iconRef;
        this.costText = costRef;
        this.selectionUI = ui;
        
        var btn = GetComponent<Button>();
        if (btn != null) Destroy(btn);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (icon != null)
        {
            icon.color = isSelected ? Color.gray : Color.white;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (selectionUI == null) return;

        if (isSelected)
        {
            selectionUI.DeselectCard(this);
        }
        else
        {
            selectionUI.SelectCard(this);
        }
    }
}

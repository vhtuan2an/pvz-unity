using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SelectionUI : MonoBehaviour
{
    [System.Serializable]
    public class RoleLayout
    {
        public GameObject panelRoot;
        public Transform contentArea;
        public Transform selectedSlotsContainer; 
        public Button readyButton;
        public TMP_Text statusText;

        [Header("Gameplay Refs")]
        public GameObject gameplayHUDPanel;
        public Transform gameplayPacketContainer;
    }

    [Header("UI Layouts")]
    [SerializeField] private RoleLayout plantLayout;
    [SerializeField] private RoleLayout zombieLayout;
    
    [Header("Prefabs & Resources")]
    [SerializeField] private GameObject plantPacketPrefab;
    [SerializeField] private GameObject zombiePacketPrefab;
    [SerializeField] private List<GameObject> allPlantPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> allZombiePrefabs = new List<GameObject>();

    private bool isReady = false;
    private Dictionary<SelectionCard, GameObject> selectedCards = new Dictionary<SelectionCard, GameObject>();
    private RoleLayout currentLayout => LobbyManager.Instance.SelectedRole == PlayerRole.Plant ? plantLayout : zombieLayout;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            OnGameStateChanged(GameStateManager.Instance.CurrentState.Value);
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
        if (plantLayout.readyButton != null) plantLayout.readyButton.onClick.RemoveListener(OnReadyClicked);
        if (zombieLayout.readyButton != null) zombieLayout.readyButton.onClick.RemoveListener(OnReadyClicked);
    }

    private void Awake()
    {
        if (plantLayout.readyButton != null) plantLayout.readyButton.onClick.AddListener(OnReadyClicked);
        if (zombieLayout.readyButton != null) zombieLayout.readyButton.onClick.AddListener(OnReadyClicked);
    }



    private void EnforceLayoutSettings(PlayerRole role)
    {
        var layout = role == PlayerRole.Plant ? plantLayout : zombieLayout;

        if (layout.contentArea != null)
        {
            GridLayoutGroup grid = layout.contentArea.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(78, 49);
            }
        }

        if (layout.selectedSlotsContainer != null)
        {
            VerticalLayoutGroup vert = layout.selectedSlotsContainer.GetComponent<VerticalLayoutGroup>();
            if (vert != null)
            {
                vert.childAlignment = TextAnchor.UpperCenter;
                vert.childControlHeight = false; 
                vert.childForceExpandHeight = false;
            }
        }

        if (layout.gameplayPacketContainer != null)
        {
            VerticalLayoutGroup vert = layout.gameplayPacketContainer.GetComponent<VerticalLayoutGroup>();
            if (vert != null)
            {
                vert.childAlignment = TextAnchor.UpperCenter;
                vert.childControlHeight = false; 
                vert.childForceExpandHeight = false;
            }
        }
    }

    private void HideSelectionUI()
    {
        if (plantLayout.panelRoot != null) plantLayout.panelRoot.SetActive(false);
        if (zombieLayout.panelRoot != null) zombieLayout.panelRoot.SetActive(false);
        
        if (LobbyManager.Instance != null)
        {
             PlayerRole role = LobbyManager.Instance.SelectedRole;
             var plantPanel = FindInactive("PlantPanel");
             var zombiePanel = FindInactive("ZombiePanel");

             if (role == PlayerRole.Plant && plantPanel != null) plantPanel.SetActive(true);
             if (role == PlayerRole.Zombie && zombiePanel != null) zombiePanel.SetActive(true);
        }
    }

    private void PopulateSelectionGrid(PlayerRole role)
    {
        var layout = role == PlayerRole.Plant ? plantLayout : zombieLayout;
        if (layout.contentArea == null) return;

        foreach (Transform child in layout.contentArea)
        {
            Destroy(child.gameObject);
        }

        List<GameObject> prefabsToUse = role == PlayerRole.Plant ? allPlantPrefabs : allZombiePrefabs;
        GameObject packetPrefabToUse = role == PlayerRole.Plant ? plantPacketPrefab : zombiePacketPrefab;
        
        foreach (var unitPrefab in prefabsToUse)
        {
            GameObject packetObj = Instantiate(packetPrefabToUse, layout.contentArea);
            packetObj.transform.localScale = Vector3.one;
            RectTransform rt = packetObj.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(78, 49);

            Image icon = null;
            TMP_Text cost = null;
            
            if (role == PlayerRole.Plant)
            {
                var seedPacket = packetObj.GetComponent<SeedPacket>();
                if (seedPacket != null)
                {
                    icon = seedPacket.icon;
                    cost = seedPacket.costText;
                    
                    var plantBase = unitPrefab.GetComponent<PlantBase>();
                    if (plantBase != null)
                    {
                        if (icon != null) icon.sprite = plantBase.packetImage != null ? plantBase.packetImage : unitPrefab.GetComponentInChildren<SpriteRenderer>()?.sprite;
                        if (cost != null) cost.text = plantBase.sunCost.ToString();
                    }
                    Destroy(seedPacket);
                }
            }
            else 
            {
                var zombiePacket = packetObj.GetComponent<ZombiePacket>();
                if (zombiePacket != null)
                {
                    icon = zombiePacket.icon;
                    cost = zombiePacket.costText;
                    
                     var zombieBase = unitPrefab.GetComponent<ZombieBase>();
                     if (zombieBase != null)
                    {
                        if (icon != null) icon.sprite = zombieBase.packetImage != null ? zombieBase.packetImage : unitPrefab.GetComponentInChildren<SpriteRenderer>()?.sprite;
                        if (cost != null) cost.text = zombieBase.GetBrainCost().ToString();
                    }
                    Destroy(zombiePacket);
                }
            }
            var selectionCard = packetObj.AddComponent<SelectionCard>();
            selectionCard.Initialize(unitPrefab, icon, cost, this);
        }
    }

    public void SelectCard(SelectionCard card)
    {
        var layout = currentLayout;
        if (layout.selectedSlotsContainer == null) return;

        if (selectedCards.Count >= 7) return;
        if (selectedCards.ContainsKey(card)) return;

        GameObject visual = CreateSlotVisual(card, layout.selectedSlotsContainer);
        card.SetSelected(true);
        selectedCards.Add(card, visual);
    }

    public void DeselectCard(SelectionCard card)
    {
        if (!selectedCards.ContainsKey(card)) return;
        GameObject visual = selectedCards[card];
        Destroy(visual);

        selectedCards.Remove(card);
        card.SetSelected(false);
    }

    private GameObject CreateSlotVisual(SelectionCard card, Transform container)
    {
        PlayerRole role = LobbyManager.Instance.SelectedRole;
        GameObject packetPrefabToUse = role == PlayerRole.Plant ? plantPacketPrefab : zombiePacketPrefab;
        
        GameObject visual = Instantiate(packetPrefabToUse, container);
        visual.name = "SelectedVisual";
        visual.transform.localScale = Vector3.one; 
        
        RectTransform rt = visual.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(78, 49);
        
        Image icon = null;
        TMP_Text cost = null;
        
        if (role == PlayerRole.Plant)
        {
            var seedPacket = visual.GetComponent<SeedPacket>();
            if (seedPacket != null) { icon = seedPacket.icon; cost = seedPacket.costText; Destroy(seedPacket); }
        }
        else
        {
            var zombiePacket = visual.GetComponent<ZombiePacket>();
            if (zombiePacket != null) { icon = zombiePacket.icon; cost = zombiePacket.costText; Destroy(zombiePacket); }
        }

        if (icon != null) icon.sprite = card.IconSprite;
        if (cost != null) cost.text = card.CostValue;

        Button btn = visual.GetComponent<Button>();
        if(btn == null) btn = visual.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => DeselectCard(card));
        
        return visual;
    }

    // Helper to find hidden objects
    private GameObject FindInactive(string name)
    {
        Transform[] objs = Resources.FindObjectsOfTypeAll<Transform>() as Transform[];
        foreach (Transform t in objs)
        {
            if (t.hideFlags == HideFlags.None && t.name == name)
            {
                return t.gameObject;
            }
        }
        return null;
    }
    
    private void ClearSelectedSlots()
    {
        selectedCards.Clear();
        ClearSlotsInLayout(plantLayout);
        ClearSlotsInLayout(zombieLayout);
    }

    private void ClearSlotsInLayout(RoleLayout layout)
    {
        if (layout.selectedSlotsContainer != null)
        {
             foreach (Transform child in layout.selectedSlotsContainer)
             {
                 Destroy(child.gameObject);
             }
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Selection)
        {
            ShowSelectionUI();
        }
        else if (newState == GameStateManager.GameState.Playing)
        {
            SetGameplayPacketsActive(true);
            HideSelectionUI();
            
            // Re-enable the gameplay HUD for the current role
            if (currentLayout.gameplayHUDPanel != null)
                currentLayout.gameplayHUDPanel.SetActive(true);
        }
        else
        {
            HideSelectionUI();
        }
    }

    private void SetGameplayPacketsActive(bool active)
    {
        if (plantLayout.gameplayPacketContainer != null)
        {
            foreach(Transform t in plantLayout.gameplayPacketContainer) t.gameObject.SetActive(active);
        }
        if (zombieLayout.gameplayPacketContainer != null)
        {
             foreach(Transform t in zombieLayout.gameplayPacketContainer) t.gameObject.SetActive(active);
        }
    }

    private void ShowSelectionUI()
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("LobbyManager.Instance is null. Cannot determine player role.");
            return;
        }

        PlayerRole role = LobbyManager.Instance.SelectedRole;

        // Hide Gameplay HUD
        if (plantLayout.gameplayHUDPanel != null) plantLayout.gameplayHUDPanel.SetActive(false);
        if (zombieLayout.gameplayHUDPanel != null) zombieLayout.gameplayHUDPanel.SetActive(false);

        // Activate the correct panel
        if (plantLayout.panelRoot != null) plantLayout.panelRoot.SetActive(role == PlayerRole.Plant);
        if (zombieLayout.panelRoot != null) zombieLayout.panelRoot.SetActive(role == PlayerRole.Zombie);

        // Populate the grid for the current role
        EnforceLayoutSettings(role);
        PopulateSelectionGrid(role);

        // Clear any previously selected cards and their visuals
        ClearSelectedSlots();

        isReady = false; // Reset ready state when showing selection UI
        UpdateStatusText();
        SetGameplayPacketsActive(false);
    }

    private void OnReadyClicked()
    {
        if (isReady) return;

        isReady = true;
        UpdateStatusText();
        
        SendSelectedDeck();

        if (NetworkManager.Singleton != null)
        {
            GameStateManager.Instance.SetPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId, true);
        }
    }

    private void SendSelectedDeck()
    {
        var layout = currentLayout;
        if (layout.gameplayPacketContainer == null) 
        {
            Debug.LogWarning("SelectionUI: No Gameplay Packet Container assigned! Cannot transfer deck.");
            return;
        }

        // Clear existing placeholders if available
        foreach (Transform child in layout.gameplayPacketContainer)
        {
             Destroy(child.gameObject);
        }

        PlayerRole role = LobbyManager.Instance.SelectedRole;
        GameObject packetPrefabToUse = role == PlayerRole.Plant ? plantPacketPrefab : zombiePacketPrefab;

        Dictionary<GameObject, SelectionCard> visualToCardMap = new Dictionary<GameObject, SelectionCard>();
        foreach(var kvp in selectedCards)
        {
            visualToCardMap[kvp.Value] = kvp.Key;
        }

        if (layout.selectedSlotsContainer != null)
        {
            foreach (Transform visualSlot in layout.selectedSlotsContainer)
            {
                if (visualToCardMap.TryGetValue(visualSlot.gameObject, out SelectionCard card))
                {
                    GameObject newPacket = Instantiate(packetPrefabToUse, layout.gameplayPacketContainer);
                    newPacket.transform.localScale = Vector3.one;
                    newPacket.SetActive(false); // Hide until game starts
                    
                    if (role == PlayerRole.Plant)
                    {
                        var seedPacket = newPacket.GetComponent<SeedPacket>();
                        if (seedPacket != null) seedPacket.AssignPlant(card.UnitPrefab);
                    }
                    else
                    {
                        var zombiePacket = newPacket.GetComponent<ZombiePacket>();
                        if (zombiePacket != null) zombiePacket.AssignZombie(card.UnitPrefab);
                    }
                }
            }
        }
        
        Debug.Log($"SelectionUI: Transferred {selectedCards.Count} cards to gameplay HUD (Hidden until Playing).");
    }

    // TODO: Temp status text will change to icon later
    private void UpdateStatusText()
    {
        var layout = currentLayout;
        
        // Auto-create status text if missing but button exists
        if (layout.statusText == null && layout.readyButton != null)
        {
             // Try to find existing first
             layout.statusText = layout.readyButton.GetComponentInChildren<TMP_Text>();
             
             // Check if it's still null, create it
             if (layout.statusText == null)
             {
                 GameObject textObj = new GameObject("StatusText");
                 textObj.transform.SetParent(layout.readyButton.transform, false);
                 
                 RectTransform rt = textObj.AddComponent<RectTransform>();
                 rt.anchorMin = Vector2.zero;
                 rt.anchorMax = Vector2.one;
                 rt.offsetMin = Vector2.zero;
                 rt.offsetMax = Vector2.zero;
                 
                 layout.statusText = textObj.AddComponent<TextMeshProUGUI>();
                 layout.statusText.alignment = TextAlignmentOptions.Center;
                 layout.statusText.fontSize = 24;
                 layout.statusText.color = Color.black; 
             }
        }

        if (layout.statusText != null)
        {
            layout.statusText.text = isReady ? "WAITING..." : "READY!";
        }
        
        if (layout.readyButton != null)
        {
            layout.readyButton.interactable = !isReady;
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Auto Populate Prefabs")]
    private void AutoPopulatePrefabs()
    {
        allPlantPrefabs.Clear();
        allZombiePrefabs.Clear();

        string[] plantGuids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefabs/Plants" });
        foreach (string guid in plantGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go.GetComponent<PlantBase>() != null) allPlantPrefabs.Add(go);
        }
        
        string[] zombieGuids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefabs/Zombies" });
        foreach (string guid in zombieGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go.GetComponent<ZombieBase>() != null) allZombiePrefabs.Add(go);
        }
        
        Debug.Log($"Auto-Populated: {allPlantPrefabs.Count} Plants, {allZombiePrefabs.Count} Zombies.");
        EditorUtility.SetDirty(this);
    }
#endif
}

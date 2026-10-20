using System.Collections.Generic;
using UnityEngine;

public class TruckGridLayout : MonoBehaviour
{
    [SerializeField] private FoodTruckInteriorTrigger interior;
    [SerializeField, Min(0.1f)] private float cellSize = 0.9f;
    [SerializeField, Range(1, 32)] private int columns = 6;
    [SerializeField, Range(1, 32)] private int rows = 4;
    [SerializeField, Tooltip("网格左下角，相对餐车坐标。")]
    private Vector2 offset = new Vector2(-2.7f, -1.65f);
    [SerializeField, Tooltip("预留给不可移动设施的格子区域；可移动大炮由工具占位管理。")]
    private List<RectInt> reservedAreas = new List<RectInt>();
    [SerializeField, HideInInspector] private bool movableCannonLayoutInitialized;
    [SerializeField] private List<PlaceableKitchenTool> tools = new List<PlaceableKitchenTool>();
    [SerializeField] private Material lineMaterial;
    private GameObject gridVisual;
    private LineRenderer preview;
    public FoodTruckInteriorTrigger Interior => interior;
    public IReadOnlyList<PlaceableKitchenTool> Tools => tools;

    public void Configure(FoodTruckInteriorTrigger truckInterior, Material material)
    { interior = truckInterior; lineMaterial = material; }
    public void Register(PlaceableKitchenTool tool) { if (!tools.Contains(tool)) tools.Add(tool); }
    public void Unregister(PlaceableKitchenTool tool) => tools.Remove(tool);

    private void Start()
    {
        if (!ValidateLayout(out string reason))
        { Debug.LogError("TruckGridLayout: " + reason, this); return; }
        foreach (var tool in tools) if (tool != null) tool.SnapToGrid();
    }

    public Vector3 CellCenter(Vector2Int cell, Vector2Int size) => transform.TransformPoint(
        new Vector3(offset.x + (cell.x + size.x * 0.5f) * cellSize,
            offset.y + (cell.y + size.y * 0.5f) * cellSize, 0f));

    public Vector2Int CellForCenter(Vector3 world, Vector2Int size)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        return new Vector2Int(Mathf.RoundToInt((local.x - offset.x) / cellSize - size.x * .5f),
            Mathf.RoundToInt((local.y - offset.y) / cellSize - size.y * .5f));
    }

    private RectInt Area(PlaceableKitchenTool tool, Vector2Int cell) => new RectInt(cell, tool.Footprint);
    private bool Fits(RectInt area)
    {
        if (area.xMin < 0 || area.yMin < 0 || area.xMax > columns || area.yMax > rows) return false;
        foreach (var reserved in reservedAreas) if (area.Overlaps(reserved)) return false;
        return true;
    }

    private bool IsClear(RectInt area, PlaceableKitchenTool a, PlaceableKitchenTool b = null)
    {
        foreach (var other in tools)
            if (other != null && other != a && other != b && area.Overlaps(Area(other, other.Cell))) return false;
        return true;
    }

    public bool ValidateLayout(out string reason)
    {
        if (interior == null || lineMaterial == null) { reason = "Assign truck interior and grid line material."; return false; }
        foreach (var tool in tools)
        {
            if (tool == null) continue;
            if (tool.Grid != this || !Fits(Area(tool, tool.Cell)) || !IsClear(Area(tool, tool.Cell), tool))
            { reason = "Invalid initial grid cell / footprint for " + tool.name; return false; }
        }
        reason = ""; return true;
    }

    public bool EvaluatePlacement(PlaceableKitchenTool tool, Vector2Int requested,
        out Vector2Int destination, out PlaceableKitchenTool swap, out string reason)
    {
        destination = requested; swap = null;
        if (tool == null || tool.Grid != this || !tool.CanMove)
        { reason = "This tool cannot be moved."; return false; }
        var area = Area(tool, requested);
        if (!Fits(area)) { reason = "Outside kitchen grid or reserved facility area."; return false; }
        foreach (var other in tools)
        {
            if (other == null || other == tool || !area.Overlaps(Area(other, other.Cell))) continue;
            if (swap != null) { reason = "More than one tool blocks this space."; return false; }
            swap = other;
        }
        if (swap != null)
        {
            if (!swap.CanMove || swap.IsCarried) { reason = "Blocked tool is fixed or contains food."; return false; }
            destination = swap.Cell;
            var targetArea = Area(tool, destination);
            var returnArea = Area(swap, tool.Cell);
            if (!Fits(targetArea) || !Fits(returnArea) || targetArea.Overlaps(returnArea) ||
                !IsClear(targetArea, tool, swap) || !IsClear(returnArea, tool, swap))
            { reason = "Both tools must fit after swapping."; return false; }
        }
        reason = swap != null ? "E: swap tools" : "E: place tool";
        return true;
    }

    public bool TryPlace(PlaceableKitchenTool tool, Vector2Int cell, out string reason)
    {
        if (tool == null || !tool.IsCarried) { reason = "No carried tool."; return false; }
        if (!EvaluatePlacement(tool, cell, out var destination, out var swap, out reason)) return false;
        Vector2Int originalCell = tool.Cell;
        if (swap != null) swap.CommitCell(originalCell);
        tool.CommitCell(destination);
        reason = swap != null ? "Tools swapped." : "Tool placed.";
        return true;
    }

    public bool HasSpaceFor(Vector2Int footprint)
    {
        for (int y = 0; y <= rows - footprint.y; y++)
        for (int x = 0; x <= columns - footprint.x; x++)
        {
            var area = new RectInt(new Vector2Int(x, y), footprint);
            if (Fits(area) && IsClear(area, null)) return true;
        }
        return false;
    }

    public bool CanPlaceNew(PlaceableKitchenTool tool, Vector2Int cell, out string reason)
    {
        if (tool == null || tool.Grid != this || !tool.CanMove ||
            !Fits(Area(tool, cell)) || !IsClear(Area(tool, cell), tool))
        { reason = "Choose an empty area inside the kitchen grid."; return false; }
        reason = "E: confirm upgrade placement"; return true;
    }

    public bool TryPlaceNew(PlaceableKitchenTool tool, Vector2Int cell, out string reason)
    {
        if (tool == null || !tool.IsCarried || !CanPlaceNew(tool, cell, out reason))
        { reason = "Choose an empty area inside the kitchen grid."; return false; }
        tool.CommitCell(cell);
        Register(tool);
        reason = "Upgrade placed."; return true;
    }

    public void ShowGrid(bool visible)
    {
        if (!visible) { if (gridVisual != null) gridVisual.SetActive(false); return; }
        if (gridVisual != null) Destroy(gridVisual);
        gridVisual = new GameObject("KitchenGridPreview"); gridVisual.transform.SetParent(transform, false);
        for (int x = 0; x <= columns; x++) MakeLine("Column", new Vector3(offset.x + x * cellSize, offset.y, 0), new Vector3(offset.x + x * cellSize, offset.y + rows * cellSize, 0));
        for (int y = 0; y <= rows; y++) MakeLine("Row", new Vector3(offset.x, offset.y + y * cellSize, 0), new Vector3(offset.x + columns * cellSize, offset.y + y * cellSize, 0));
        foreach (var reserved in reservedAreas) Outline("Reserved", reserved, new Color(1f, .6f, .15f));
        preview = Outline("Placement", new RectInt(0, 0, 1, 1), Color.green);
        preview.enabled = false;
    }

    private void MakeLine(string name, Vector3 a, Vector3 b)
    {
        var line = NewLine(name); line.startColor = line.endColor = new Color(.5f, .8f, .9f, .55f);
        line.positionCount = 2; line.SetPosition(0, a); line.SetPosition(1, b);
    }
    private LineRenderer NewLine(string name)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(gridVisual.transform, false);
        line.sharedMaterial = lineMaterial; line.useWorldSpace = false; line.widthMultiplier = .025f; line.sortingOrder = 20;
        return line;
    }
    private LineRenderer Outline(string name, RectInt area, Color color)
    {
        var line = NewLine(name); line.loop = true; line.positionCount = 4;
        SetOutline(line, area); line.startColor = line.endColor = color; line.widthMultiplier = .045f;
        return line;
    }
    private void SetOutline(LineRenderer line, RectInt area)
    {
        float x = offset.x + area.x * cellSize, y = offset.y + area.y * cellSize;
        line.SetPositions(new[] { new Vector3(x,y,0), new Vector3(x+area.width*cellSize,y,0),
            new Vector3(x+area.width*cellSize,y+area.height*cellSize,0), new Vector3(x,y+area.height*cellSize,0) });
    }
    public void ShowPlacement(PlaceableKitchenTool tool, Vector2Int cell, bool newPurchase = false)
    {
        if (preview == null) return;
        preview.enabled = tool != null;
        if (tool == null) return;
        Vector2Int destination = cell;
        bool valid = newPurchase ? CanPlaceNew(tool, cell, out _) : EvaluatePlacement(tool, cell, out destination, out _, out _);
        SetOutline(preview, Area(tool, destination));
        preview.startColor = preview.endColor = valid ? Color.green : Color.red;
    }
    private void OnDisable() { if (gridVisual != null) gridVisual.SetActive(false); }
    private void OnValidate() { cellSize = Mathf.Max(.1f, cellSize); columns = Mathf.Clamp(columns, 1, 32); rows = Mathf.Clamp(rows, 1, 32); }
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(offset.x + columns * cellSize * .5f, offset.y + rows * cellSize * .5f, 0), new Vector3(columns * cellSize, rows * cellSize, 0));
        foreach (var area in reservedAreas)
        {
            Gizmos.color = new Color(1f,.6f,.15f);
            Gizmos.DrawWireCube(new Vector3(offset.x+(area.x+area.width*.5f)*cellSize, offset.y+(area.y+area.height*.5f)*cellSize, 0), new Vector3(area.width*cellSize, area.height*cellSize,0));
        }
    }
}

using TMPro;
using UnityEngine;

/// <summary>Tavla på väggen som visar hur många rätt saker som ligger i ryggsäcken.</summary>
public class ChecklistBoard : MonoBehaviour
{
    public Backpack backpack;
    public CheckShelf checkShelf;
    public TextMeshPro text;
    [Tooltip("Visa antal rätt hela tiden (annars bara efter godkänd kontroll)")]
    public bool showLive = true;

    int _lastCorrect = -1, _lastTotal = -1;
    bool _lastClosed;

    void Update()
    {
        if (backpack == null || text == null) return;
        int correct = backpack.CorrectCount;
        if (correct == _lastCorrect && backpack.TotalCategories == _lastTotal && backpack.IsClosed == _lastClosed) return;
        _lastCorrect = correct;
        _lastTotal = backpack.TotalCategories;
        _lastClosed = backpack.IsClosed;

        int min = checkShelf != null ? checkShelf.minCorrect : 5;
        string count = showLive || backpack.IsClosed ? $"{correct} av {backpack.TotalCategories}" : "?";
        text.text =
            "<b>Packa för skyddsrummet</b>\n\n" +
            "Rätt saker i ryggsäcken:\n" +
            $"<size=160%><b>{count}</b></size>\n\n" +
            $"Minst {min} behövs.\nInga otillåtna saker får följa med.\n\n" +
            "<size=85%>Ställ ryggsäcken på incheckningshyllan\nvid dörren när du är klar.</size>";
    }
}

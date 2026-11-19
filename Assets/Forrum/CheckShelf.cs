using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Incheckningshyllan vid dörren. Ställ ryggsäcken här så kontrolleras innehållet.
/// Godkänt = inga otillåtna saker och minst <see cref="minCorrect"/> rätt saker.
/// </summary>
public class CheckShelf : MonoBehaviour
{
    public Backpack backpack;
    [Tooltip("Området ovanför hyllan där ryggsäcken ska stå")]
    public BoxCollider zone;
    public int minCorrect = 5;
    [Tooltip("Efter så här många misslyckade försök markeras felaktiga saker med rött i listan")]
    public int hintAfterFails = 2;

    [Header("Feedback")]
    public TextMeshPro sign;
    public Renderer lamp;
    public AudioSource approvedSound;
    public AudioSource failSound;
    public UnityEvent onApproved;

    public bool Approved { get; private set; }

    static readonly Color Idle = new Color(0.9f, 0.7f, 0.1f);
    static readonly Color Bad = new Color(0.85f, 0.1f, 0.08f);
    static readonly Color Good = new Color(0.1f, 0.8f, 0.2f);

    int _fails;
    float _restTime;
    bool _evaluated;

    void OnEnable() { if (backpack != null) backpack.ContentsChanged += OnContentsChanged; }
    void OnDisable() { if (backpack != null) backpack.ContentsChanged -= OnContentsChanged; }

    void Start() => ShowIdle();

    void OnContentsChanged()
    {
        // Om man tar ut saker medan väskan står på hyllan kontrolleras den igen.
        if (_evaluated && !Approved) { _evaluated = false; _restTime = 0f; }
    }

    void Update()
    {
        if (Approved || backpack == null) return;

        bool onShelf = !backpack.IsHeld && ForrumUtil.InBox(zone, backpack.transform.position + Vector3.up * 0.1f);
        if (!onShelf)
        {
            _restTime = 0f;
            if (_evaluated) { _evaluated = false; ShowIdle(); }
            return;
        }
        if (_evaluated) return;
        if (backpack.Speed > 0.05f) { _restTime = 0f; return; }

        _restTime += Time.deltaTime;
        if (_restTime > 0.5f) Evaluate();
    }

    void Evaluate()
    {
        _evaluated = true;
        int wrong = backpack.WrongCount;
        int correct = backpack.CorrectCount;

        if (wrong > 0)
        {
            _fails++;
            string msg = wrong == 1
                ? "1 sak i ryggsäcken hör inte hemma\ni ett skyddsrum. Ta ut den!"
                : $"{wrong} saker i ryggsäcken hör inte hemma\ni ett skyddsrum. Ta ut dem!";
            if (_fails >= hintAfterFails)
            {
                backpack.HighlightWrong = true;
                msg += "\n<size=80%>Tips: det som inte får följa med är rött i listan.</size>";
            }
            Show(Bad, msg);
            if (failSound != null) failSound.Play();
        }
        else if (correct < minCorrect)
        {
            Show(Bad, $"Du har packat {correct} saker som behövs.\nPacka minst {minCorrect}.");
            if (failSound != null) failSound.Play();
        }
        else
        {
            Approved = true;
            backpack.Close();
            Show(Good, "<b>Godkänt!</b>\nLyft ryggsäcken för att ta på dig den.");
            if (approvedSound != null) approvedSound.Play();
            onApproved?.Invoke();
        }
    }

    void ShowIdle() => Show(Idle, "<b>Incheckning</b>\nStäll den packade ryggsäcken här.");

    void Show(Color c, string text)
    {
        if (sign != null) sign.text = text;
        if (lamp != null) lamp.material.color = c;
    }
}

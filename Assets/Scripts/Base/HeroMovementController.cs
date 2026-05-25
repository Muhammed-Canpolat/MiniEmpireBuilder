using UnityEngine;
using DG.Tweening;

/// <summary>
/// Ana kahraman hareketi — tıklanan pozisyona DOTween ile yürüme (2 birim/sn)
/// Hareketsizken scale loop ile idle animasyon oynar
/// </summary>
public class HeroMovementController : MonoBehaviour
{
    public static HeroMovementController Instance { get; private set; }

    private Vector3 _baseScale;
    private bool    _isMoving;

    // ==================== UNITY YAŞAM DÖNGÜSÜ ====================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _baseScale = transform.localScale;
        _rb = GetComponent<Rigidbody2D>();
        PlayIdle();
    }

    private void Update()
    {
        if (_joystickInput != Vector2.zero)
        {
            if (!_isMoving)
            {
                transform.DOKill();
                _isMoving = true;
                // Yürüme animasyonu
                transform.DOScale(_baseScale * 0.95f, 0.15f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            Vector2 moveDelta = new Vector2(_joystickInput.x, _joystickInput.y) * (Time.deltaTime * 3.5f);
            if (_rb != null)
                _rb.MovePosition(_rb.position + moveDelta);
            else
                transform.position += new Vector3(moveDelta.x, moveDelta.y, 0f);

            // Yön dönme
            if (_joystickInput.x < 0) transform.rotation = Quaternion.Euler(0, 180, 0);
            else if (_joystickInput.x > 0) transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else if (_isMoving && !_isClickMoving)
        {
            _isMoving = false;
            PlayIdle();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== HAREKETLİ ANİMASYON ====================

    private void PlayIdle()
    {
        transform.DOKill();
        transform.localScale = _baseScale;
        transform
            .DOScale(_baseScale * 1.06f, 0.85f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    // ==================== API ====================

    private Vector2 _joystickInput;
    private bool _isClickMoving = false;
    private Rigidbody2D _rb;

    public void SetMoveInput(Vector2 dir)
    {
        _joystickInput = dir;
    }

    /// <summary>
    /// Dünya koordinatında verilen pozisyona yürü.
    /// Hız: 2 birim/saniye.
    /// </summary>
    public void MoveToPosition(Vector3 worldPos)
    {
        if (_joystickInput != Vector2.zero) return; // Joystick devredeyken click iptal

        float dist = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(worldPos.x, worldPos.y));

        if (dist < 0.12f) return;

        float duration = dist / 2f;
        Vector3 target = new Vector3(worldPos.x, worldPos.y, transform.position.z);

        transform.DOKill();
        _isMoving = true;
        _isClickMoving = true;

        if (target.x < transform.position.x) transform.rotation = Quaternion.Euler(0, 180, 0);
        else if (target.x > transform.position.x) transform.rotation = Quaternion.Euler(0, 0, 0);

        // Yürüme — hafif scale squash efekti
        transform.DOScale(_baseScale * 0.95f, 0.15f)
            .SetLoops(Mathf.CeilToInt(duration / 0.3f), LoopType.Yoyo)
            .SetEase(Ease.InOutSine);

        transform.DOMove(target, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                _isMoving = false;
                _isClickMoving = false;
                PlayIdle();
            });
    }

    public bool IsMoving => _isMoving;
}

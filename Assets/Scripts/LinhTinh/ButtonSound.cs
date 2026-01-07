using UnityEngine;
using UnityEngine.UI;

public class ButtonSound : MonoBehaviour
{
    void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            // Tự động thêm sự kiện PlaySFX khi nhấn nút
            btn.onClick.AddListener(() => {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
                }
            });
        }
    }
}
using TMPro;
using UnityEngine;

public class NameInputFilter : MonoBehaviour
{
    [SerializeField] TMP_InputField inputField;
    [SerializeField] int maxLength = 3;

    readonly string[] banned =
    {
        "SEX", "ASS", "FUK", "FUC", "CUM", "DIE", "DIK", "DIC", "KKK", "NAZ"
    };

    void Awake()
    {
        inputField.characterLimit = maxLength;
        inputField.onValueChanged.AddListener(OnNameChanged);
    }

    void OnNameChanged(string value)
    {
        string filtered = Filter(value);

        if (inputField.text != filtered)
            inputField.text = filtered;
    }

    string Filter(string input)
    {
        input = input.ToUpper();

        string result = "";

        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
                result += c;

            if (result.Length >= maxLength)
                break;
        }

        foreach (string word in banned)
        {
            if (result.Contains(word))
                return "";
        }

        return result;
    }
}
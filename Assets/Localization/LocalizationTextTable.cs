// Assets/Scripts/Localization/LocalizationTextTable.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum Language
{
    English,
    Japanese,
    // add more to match CSV columns
}

[Serializable]
public class LocalizedTextRow
{
    public string key;
    public string english;
    public string japanese;
}

[CreateAssetMenu(fileName = "LocalizationTextTable", menuName = "Localization/Text Table")]
public class LocalizationTextTable : ScriptableObject
{
    public List<LocalizedTextRow> rows = new();
}

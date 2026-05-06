using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Fruit Game/Level Data")]
public class LevelData : ScriptableObject
{
    public string levelName;
    public int targetScore;
    public List<FruitData> fruits = new List<FruitData>();
}
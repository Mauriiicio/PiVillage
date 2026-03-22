using System;
using System.Collections.Generic;

[Serializable]
public class FlowerData
{
    public string spriteName;
    public float x;
    public float y;
}

[Serializable]
public class FlowerDataList
{
    public List<FlowerData> flowers = new List<FlowerData>();
}

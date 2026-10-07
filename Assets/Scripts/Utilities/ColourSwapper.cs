using UnityEngine;
using System.Collections.Generic;

public class ColourSwapper : MonoBehaviour
{
    [SerializeField] private Material mat1, mat2;
    private bool currentIs2 = false;
    
    public void Swap()
    {
        List<Material> mats = new();

        mats.Add(currentIs2 ? mat2 : mat1);

        GetComponent<Renderer>().SetMaterials(mats);
        currentIs2 = !currentIs2;
    }
}

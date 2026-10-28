using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aegis.GrenadeSystem.HiEx {
public class DamageHandler : MonoBehaviour
{

    //This script is a placeholder for handling the damage applied by the explosion of the grenade.

    //You should create your own script for handling damage to your player or enemies, or else adapt this one 
    //to apply the damage incurred by the grenade explosion.

    //This is the health the object has 
    float health = 100f;


    public void ApplyDamage(float dam)
    {
        health -= dam;

        //console message to test damage taken by object due to grenade explosion
        Debug.Log(gameObject.name + " took " + dam + " Damage from the Grenade");
    }


}

}

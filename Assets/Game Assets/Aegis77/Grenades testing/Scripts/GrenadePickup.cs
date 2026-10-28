using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aegis.GrenadeSystem.HiEx
{
    public class GrenadePickup : MonoBehaviour
    {

        // this script handles picking up a grenade, and should be attatched to the grenade pickup
        // You can duplicate it for different types of grenade, to add them to an inventory system

        [SerializeField] AudioClip grenadePickupSound;

        //this logic is what happens when a palyer picks up a grenade with this script attatched
        private void OnTriggerEnter(Collider other)
        {
            //if the player collides with the Grenade pickup, refrerence the Grenade Inventory System and add a grenade to it
            if (other.tag == "Player")
            {
                //add grenade to inventory
                other.GetComponent<GrenadeSystem>().PickupGrenade();

                //play pickup sound
                AudioSource soundSource = other.GetComponent<AudioSource>();
                soundSource.clip = grenadePickupSound;
                soundSource.Play();

                //destory the pickup object
                Destroy(gameObject);

            }
        }

    }
}
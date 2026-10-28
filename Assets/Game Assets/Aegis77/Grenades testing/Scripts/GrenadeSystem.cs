using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Aegis.GrenadeSystem.HiEx
{
    public class GrenadeSystem : MonoBehaviour
    {

        //This system handles the grenade inventory (count) and throwing mechanic
        [Header("Grenade throwing system settings")]
        [SerializeField] GameObject player;
        [SerializeField] Transform throwPoint;
        [SerializeField] Transform camera;
        [SerializeField] GameObject hiexgrenade;
        [SerializeField] GameObject grenadecountUI;
        [SerializeField] AudioClip throwAudio;

        //throwing settings
        [SerializeField] int grenadeCount = 3; // number of grenades in inventory by default
        [SerializeField] float throwDelay = 0.3f; // delay after pressing throw before grenade is thrown to account for animations/audio
        [SerializeField] float throwForce = 4f; // tweak this to adjust how far the grenade is thrown


        Coroutine throwGrenade = null;


        //When the 'G' key is pressed, a grenade is thrown if grenadeCount is more than 0
        // You can update this to use the input system or key that you'd prefer


        private void Start()
        {
            // set grenade count in UI
            UpdateGrenadeCount();
        }


        private void Update()
        {

            //When G is pressed
            if (Input.GetKeyDown(KeyCode.G))
            {

                //Check if a grenade is already being thrown - if not, and there are grenades in the inventory, throw a grenade
                // This is so grenades can't be spammed

                if (grenadeCount > 0 && throwGrenade == null)
                {
                    throwGrenade = StartCoroutine(ThrowGrenade());
                }
            }
        }


        //This is the grenade throwing Co-routine which handles the actions of throwing a grenade
        IEnumerator ThrowGrenade()
        {
            //decrement the count of grenades in the inventory
            grenadeCount -= 1;

            UpdateGrenadeCount();

            //play audio of throwing a grenade
            player.GetComponent<AudioSource>().clip = throwAudio;
            player.GetComponent<AudioSource>().Play();

            //wait for audio to begin playing - this delay is to account for the pin being pulled, but you can edit it above
            yield return new WaitForSeconds(throwDelay);

            //throw the grenade
            GameObject grenadeInstance = Instantiate(hiexgrenade, throwPoint.position, throwPoint.rotation);

            Rigidbody rb = grenadeInstance.GetComponent<Rigidbody>();

            rb.AddForce(camera.forward * throwForce, ForceMode.Impulse);

            //set throwGrenade verable to null, so another grenade can be thrown

            throwGrenade = null;
        }

        // Function to add a grenade to the inventory when one is picked up
        public void PickupGrenade()
        {
            grenadeCount += 1;

            UpdateGrenadeCount();
        }

        //Function to update the number of grenades displayed in the UI
        void UpdateGrenadeCount()
        {
            grenadecountUI.GetComponent<TMPro.TextMeshProUGUI>().text = grenadeCount.ToString();
        }

    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aegis.GrenadeSystem.HiEx
{
    public class PlayerController : MonoBehaviour
    {
        //This is a simple first person controller, and is for testing purposes only.
        //You can adapt this controller for your own game, or copy the mechanics used to throw the grenade, using your own input system
        Vector2 look;
        Vector3 velocity;
        CharacterController controller;

        [Header("First Person Contoller settings")]
        [SerializeField] float mouseSensitivity = 3f;
        [SerializeField] float movementSpeed = 3f;
        [SerializeField] float playerMass = 1f;
        [SerializeField] float jumpSpeed = 5f;
        [SerializeField] Transform cameraTransform;


        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }


        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }


        void Update()
        {
            UpdateLook();
            UpdateMovement();
            UpdateGravity();
        }

        void UpdateMovement()
        {
            var x = Input.GetAxis("Horizontal");
            var y = Input.GetAxis("Vertical");

            var input = new Vector3();
            input += transform.forward * y;
            input += transform.right * x;
            input = Vector3.ClampMagnitude(input, 1f);

            if (Input.GetButtonDown("Jump") && controller.isGrounded)
            {
                velocity.y += jumpSpeed;
            }

            controller.Move((input * movementSpeed + velocity) * Time.deltaTime);
        }


        void UpdateLook()
        {
            look.x += Input.GetAxis("Mouse X") * mouseSensitivity;
            look.y += Input.GetAxis("Mouse Y") * mouseSensitivity;

            look.y = Mathf.Clamp(look.y, -89f, 89f);

            cameraTransform.localRotation = Quaternion.Euler(-look.y, 0, 0);
            transform.localRotation = Quaternion.Euler(0, look.x, 0);
        }

        void UpdateGravity()
        {
            var gravity = Physics.gravity * playerMass * Time.deltaTime;

            velocity.y = controller.isGrounded ? -1f : velocity.y + gravity.y;
        }
    }
}
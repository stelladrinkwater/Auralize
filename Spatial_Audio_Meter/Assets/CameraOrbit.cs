//  A simple Unity C# script for orbital movement around a target gameobject
//  Author: Ashkan Ashtiani
//  Gist on Github: https://gist.github.com/3dln/c16d000b174f7ccf6df9a1cb0cef7f80

using System;
using UnityEngine;

namespace TDLN.CameraControllers {
    public class CameraOrbit : MonoBehaviour {
        public GameObject target;
        public GameObject head;
        public float distance = 10.0f;

        public float xSpeed = 250.0f;
        public float ySpeed = 120.0f;

        public float randomSpeed = 0.01f;
        public float randomPhase = 0;
        public float circleRadius = 0.1f;

        public float yMinLimit = -20;
        public float yMaxLimit = 80;

        float x = 0.0f;
        float y = 0.0f;

        private bool isMousePressed = false;

        void Start() {
            var angles = transform.eulerAngles;
            x = angles.y;
            y = angles.x;
        }

        float prevDistance;

        void LateUpdate() {
            if (distance < 0.2f) distance = 0.2f;
            distance -= Input.GetAxis("Mouse ScrollWheel") * 2;

            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) {
                isMousePressed = true;
            }
            if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) {
                isMousePressed = false;
            }

            if (isMousePressed) {
                var pos = Input.mousePosition;
                var dpiScale = 1f;
                if (Screen.dpi < 1) dpiScale = 1;
                if (Screen.dpi < 200) dpiScale = 1;
                else dpiScale = Screen.dpi / 200f;

                if (pos.x < 380 * dpiScale && Screen.height - pos.y < 250 * dpiScale) return;

                x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

                y = ClampAngle(y, yMinLimit, yMaxLimit);
                var rotation = Quaternion.Euler(y, x, 0);
                var position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.transform.position;
                transform.rotation = rotation;
                transform.position = position;
            }
            else {
                UpdateRandomMovement();
            }

            if (Math.Abs(prevDistance - distance) > 0.001f) {
                prevDistance = distance;
                var rot = Quaternion.Euler(y, x, 0);
                var po = rot * new Vector3(0.0f, 0.0f, -distance) + target.transform.position;
                transform.rotation = rot;
                transform.position = po;
            }
        }

        private void UpdateRandomMovement() {
            randomPhase += randomSpeed * Time.deltaTime;
            float randomX = Mathf.Sin(randomPhase) * circleRadius;
            float randomY = Mathf.Cos(randomPhase) * circleRadius;

            x += randomX * 0.01f;
            y += randomY * 0.01f;
            y = ClampAngle(y, yMinLimit, yMaxLimit);
            var rotation = Quaternion.Euler(y, x, 0);
            var position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.transform.position;
            transform.rotation = rotation;
            transform.position = position;
        }

        float ClampAngle(float angle, float min, float max) {
            if (angle < -360f) angle += 360f;
            if (angle > 360f) angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }

        // Simple script to update the head position with a bit of damping
        void UpdateHeadPosition() {
            if (head == null) return;
            var headRot = head.transform.rotation;
            var cameraRot = transform.rotation;

            var rot = Quaternion.Slerp(headRot, cameraRot, Time.deltaTime * 7.5f);

            head.transform.rotation = rot;
        }
        void Update() {
            UpdateHeadPosition();
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimpleJSON; // JSONNode

namespace JustAnotherUser {
    class CustomCollisionTrigger : CustomTrigger {
        private static readonly string COLLISION_BOX_NAME = "TI_collision_{uuid}";
        private static readonly float SECONDS_PER_UPDATE = 0.4f;

        private TriggerAction _callback;
        private Component _focus;
        private Vector3 _offset;
        private float? _size;

        private float _secondsSinceLastUpdate;
        private List<Atom> _lastAtomsColliding;

        private Atom _collisionBox; // area where to detect collision

        public CustomCollisionTrigger(TriggerAction callback, Component focus, Vector3 offset, float size) {
            this._lastAtomsColliding = new List<Atom>();

            this.SetCallback(callback);
            this.SetFocus(focus, offset, size);
        }

        public void SetCallback(TriggerAction callback) {
            this._callback = callback;

            if (this._size != null) this.AllSettersSetted();
        }

        public void SetFocus(Component focus, Vector3 offset, float size) {
            this._focus = focus;
            this._offset = offset;
            this._size = size;

            if (this._callback != null) this.AllSettersSetted();
        }

        public void SetFocus(Vector3 offset) {
            this._offset = offset;
        }

        private void AllSettersSetted() {
            this._callback.RunCoroutine(GetCollisionBox(COLLISION_BOX_NAME.Replace("{uuid}", this._callback.GetUUID())));
        }

        public void Update() {
            if (this._collisionBox == null) return; // wait to load
            this._collisionBox.mainController.transform.position = this._focus.transform.TransformPoint(this._offset);

            this._secondsSinceLastUpdate += Time.deltaTime;
            if (this._secondsSinceLastUpdate >= SECONDS_PER_UPDATE) {
                this._secondsSinceLastUpdate = 0;

                List<Atom> collidingAtoms = this.GetDesiredAtomsColliding();
                List<Atom> newCollidingAtoms = new List<Atom>(collidingAtoms),
                        notCollidingAnymoreAtoms = new List<Atom>(this._lastAtomsColliding);
                newCollidingAtoms.RemoveAll(a => this._lastAtomsColliding.Contains(a));
                notCollidingAnymoreAtoms.RemoveAll(a => collidingAtoms.Contains(a));
                this._lastAtomsColliding = collidingAtoms;

                foreach (Atom newCollider in newCollidingAtoms) {
                    this._callback.OnCollision(newCollider);
                }

                foreach (Atom releaseCollider in notCollidingAnymoreAtoms) {
                    this._callback.OnRelease(releaseCollider);
                }
            }
        }

        private void SetupCollisionBox() {
            this._collisionBox.hidden = true;
            this._collisionBox.GetStorableByID("scale").GetFloatJSONParam("scale").val = (float)this._size;

            // we don't need callbacks; we check it every time
            /*JSONStorable trigger = this._collisionBox.GetStorableByID("Trigger");
            JSONClass triggerJSON = trigger.GetJSON();

            if (triggerJSON["trigger"]["startActions"].AsArray.Count == 0) {
                triggerJSON["trigger"]["startActions"][0].Add("receiverAtom", "");
                triggerJSON["trigger"]["startActions"][0].Add("receiver", "");
                triggerJSON["trigger"]["startActions"][0].Add("receiverTargetName", "");
                triggerJSON["trigger"]["startActions"][0].Add("boolValue", "");
            }
            triggerJSON["trigger"]["startActions"][0]["receiverAtom"].Value = this.containingAtom.name;
            triggerJSON["trigger"]["startActions"][0]["receiver"].Value = this.storeId; // plugin ID
            triggerJSON["trigger"]["startActions"][0]["receiverTargetName"].Value = "run";
            triggerJSON["trigger"]["startActions"][0]["boolValue"].Value = "true";
            trigger.LateRestoreFromJSON(triggerJSON);*/ // TODO what will happend with the older object? will it keep the JSON?
        }
        private static List<Atom> GetCollidingAtoms(CollisionTrigger trigger) {
            CollisionTriggerEventHandler handler = trigger.GetComponentInChildren<CollisionTriggerEventHandler>();

            List<Atom> r = new List<Atom>();
            if (handler == null) return r;

            foreach (KeyValuePair<Collider,bool> collider in handler.collidingWithDictionary) {
                Atom collided = SuperController.singleton.GetAtoms().FirstOrDefault(a =>
                        a.GetComponentsInChildren<Collider>().FirstOrDefault(c => c == collider.Key) != null);
                if (collided != null) r.Add(collided);
            }
            return r;
        }

        private List<Atom> GetDesiredAtomsColliding() {
            if (this._collisionBox == null) return new List<Atom>();

            List<Atom> colliding = CustomCollisionTrigger.GetCollidingAtoms(this._collisionBox.GetComponentInChildren<CollisionTrigger>()); // TODO use collision box wrapper
            colliding.RemoveAll(a => !this._callback.IsDesired(a));
            return colliding;
        }

        private IEnumerator GetCollisionBox(string name) {
            // does it already exists?
            this._collisionBox = SuperController.singleton.GetAtoms().FirstOrDefault(a => a.name == name);
            if (this._collisionBox == null) {
                // no collision box; generate a new one
                SuperController.LogMessage("Generating collision box...");

                yield return SuperController.singleton.AddAtomByType("CollisionTrigger", name);
                this._collisionBox = SuperController.singleton.GetAtoms().FirstOrDefault(a => a.name == name);
            }

            this.SetupCollisionBox();
        }
    }
}

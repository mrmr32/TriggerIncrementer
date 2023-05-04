using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimpleJSON; // JSONNode
using MacGruber;

namespace JustAnotherUser {
    public class TriggerIncrementer : MVRScript, TriggerIncrementerActions, TriggerAction {
        private List<DAZMorph> _morphs, _usedMorphs;
        private List<SceneObject> _colliders; // the object that will colide with the collider region
        private MotionAnimationControl _head;
        private DAZCharacterSelector _characterSelector;

        private TriggerIncrementerUI _ui;

        private float _duration;

        private float _finalTime = 0f; // play the animation until finalTime
        private CustomTrigger _customTrigger;

        private static readonly float COLLISION_BOX_SIZE = 0.4f;

        private static readonly string VERSION = "2.0";

        public override void Init() {
            // plugin VaM GUI description
            pluginLabelJSON.val = "TriggerIncrementer v" + VERSION;

            // get the objects
            if (containingAtom.type != "Person" || (this._characterSelector = containingAtom.GetComponentInChildren<DAZCharacterSelector>()) == null) {
                throw new InvalidOperationException("Missing DAZCharacterSelector");
            }

            if ((this._head = GetHead()) == null) throw new InvalidOperationException("Head not found in added object");

            this._ui = new TriggerIncrementerUI(this, this);

            this._morphs = new List<DAZMorph>();
            this._usedMorphs = new List<DAZMorph>();

            this._customTrigger = new CustomCollisionTrigger(this, this._head, Vector3.down * 0.05f, COLLISION_BOX_SIZE);

            SimpleTriggerHandler.LoadAssets();
            this._ui.InitUI();
        }


        // Runs once when plugin loads (after Init)
        protected void Start() {
            this._morphs.Clear();
            ScanBank(this._characterSelector.morphBank1, this._morphs); // @author https://github.com/ProjectCanyon/morph-merger/blob/master/MorphMerger.cs
            ScanBank(this._characterSelector.morphBank2, this._morphs);
            ScanBank(this._characterSelector.morphBank3, this._morphs);

            //SuperController.LogMessage(pluginLabelJSON.val + " Loaded");

            this._ui.LoadJson(GetPluginJsonFromSave());
        }

        public string GetUUID() {
            return this.storeId + "_" + containingAtom.name;
        }

        private void AddStepToAnimation() {
            this._ui.GetTriggerInvoker()();
            if (this._finalTime > Time.time) this._finalTime += this._duration;
            else this._finalTime = Time.time + this._duration;
        }

        public void OnCollision(Atom collide) {
            if (this._ui.GetRemoveOnCollision()) {
                collide.Remove();
                this.AddStepToAnimation();
            }
            else {
                if (!this._ui.GetCumulativeCollision()) this.AddStepToAnimation(); // just one time
                else this.InvokeRepeating("AddStepToAnimation", 0f, 1f); ; // each second until OnRelease
            }
        }

        public void OnRelease(Atom collide) {
            if (!this._ui.GetRemoveOnCollision() && this._ui.GetCumulativeCollision()) this.CancelInvoke("AddStepToAnimation"); // each second is being called
        }
        
        public bool IsDesired(Atom a) {
            if (this._colliders.Count < 1) return false;
            return this._colliders.Any(obj => obj.Equals(a.name));
        }

        public void SetTargetObjects(List<SceneObject> objects) {
            this._colliders = objects;

            //SuperController.LogMessage("New collider");
        }

        public void AnimationDurationChanged(float newDuration) {
            this._duration = newDuration;
        }
        
        public void OffsetChanged(float down, float front) {
            this._customTrigger.SetFocus(Vector3.down * down + Vector3.forward * front);
        }

        public void Update() {
            this._ui.Update();
            this._customTrigger.Update();

            if (this._finalTime < Time.time) return;
            float secondsSinceLastUpdate = Time.deltaTime;
            foreach (DAZMorph morph in this._usedMorphs) {
                float addPerAnimation = this._ui.GetMorphIncrement(morph.displayName),
                    addPerSecond = addPerAnimation / this._duration;
                morph.SetValue(morph.appliedValue + addPerSecond*secondsSinceLastUpdate);
                morph.SyncJSON();
            }
            // TODO check GetFloatJSONParamMaxValue (?)
        }


        private void OnAtomRename(string oldid, string newid) {
            this._ui.OnAtomRename(oldid, newid);
        }

        protected void OnDestroy() {
            this._ui.OnDestroy();
        }
        
		public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false) {
            JSONClass jc = base.GetJSON(includePhysical, includeAppearance, forceStore);
            jc = this._ui.UpdateJSON(includePhysical, includeAppearance, forceStore, jc);
            return jc;
        }
        
		public override void LateRestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, bool setMissingToDefault = true) {
            base.LateRestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);
            this._ui.LateRestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);
        }

        public void RunCoroutine(IEnumerator enumerator) {
            this.StartCoroutine(enumerator);
        }


        public List<string> GetAllMorphNames() {
            return this._morphs.Select(m => m.displayName).Distinct().ToList();
        }

        public void AddMorph(string morphName) {
            DAZMorph morph = FindMorphByName(this._morphs, morphName);
            if (morph == null) {
                SuperController.LogError("Unable to find morph '" + morphName + "'");
                return;
            }

            this._usedMorphs.Add(morph);
        }

        // @author https://raw.githubusercontent.com/ChrisTopherTa54321/VamScripts/master/FloatMultiParamRandomizer.cs
        public JSONNode GetPluginJsonFromSave() {
            foreach (JSONNode atoms in SuperController.singleton.loadJson["atoms"].AsArray) {
                if (!atoms["id"].Value.Equals(containingAtom.name)) continue;

                foreach (JSONNode storable in atoms["storables"].AsArray) {
                    if (storable["id"].Value == this.storeId) {
                        return storable;
                    }
                }
            }

            return null;
        }

        private MotionAnimationControl GetHead() {
            foreach (MotionAnimationControl mac in containingAtom.motionAnimationControls) { // TODO get head inside linkableRigidbodies?
                if (!mac.name.Equals("headControl")) continue;
                return mac;
            }

            return null; // not found
        }

        private void ScanBank(DAZMorphBank bank, List<DAZMorph> morphs) { // TODO only morph (not morph & pose)
            if (bank == null) return;

            foreach (DAZMorph morph in bank.morphs) {
                if (!morph.visible) continue;

                morphs.Add(morph);
                //SuperController.LogMessage(morph.morphName);
            }
        }

        private DAZMorph FindMorphByName(List<DAZMorph> morphs, string name) {
            foreach (DAZMorph morph in morphs) {
                if (!morph.displayName.Equals(name)) continue;

                return morph;
            }

            return null; // not found
        }
    }
}
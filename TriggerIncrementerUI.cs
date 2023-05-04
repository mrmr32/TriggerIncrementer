using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimpleJSON; // JSONNode
using UnityEngine.UI; // InputField
using MacGruber;

namespace JustAnotherUser {
    public class TriggerIncrementerUI {
        private MVRScript _script;
        private TriggerIncrementerActions _ti;

        private JSONStorableBool _destroyStorable,
                                _cumulativeStorable;
        private JSONStorableFloat _durationStorable;
        private JSONStorableFloat _offsetDownStorable,
                                _offsetFrontStorable;

        private IDictionary<string, JSONStorableFloat> _morphIncrement; // all the morphs that it should modify and its increment
        private List<JSONStorableParam> _colliderSelectors;

        private EventTrigger _events;

        public TriggerIncrementerUI(TriggerIncrementer script, TriggerIncrementerActions ti) {
            this._script = script;
            this._ti = ti;

            this._morphIncrement = new Dictionary<string, JSONStorableFloat>();
            this._colliderSelectors = new List<JSONStorableParam>();
        }

        // @ref https://hub.virtamate.com/threads/coding-canvas-and-layers-in-vam-text-inputs.2839/
        private static UIDynamicTextField CreateTextInput(MVRScript script, JSONStorableString jss, bool rightSide = false) {
            var textfield = script.CreateTextField(jss, rightSide);
            textfield.height = 20f;
            textfield.backgroundColor = Color.white;
            var input = textfield.gameObject.AddComponent<InputField>();
            var rect = input.GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 0.4f);
            input.textComponent = textfield.UItext;
            jss.inputField = input;
            return textfield;
        }

        public void InitUI() {
            this._events = new EventTrigger(this._script, "OnCollide");
            var triggersButton = this._script.CreateButton("Go to triggers ->");
            triggersButton.button.onClick.AddListener(this._events.OpenPanel);

            this._destroyStorable = new JSONStorableBool("destroy", true);
            this._script.RegisterBool(this._destroyStorable);
            UIDynamicToggle destroyUI = this._script.CreateToggle(this._destroyStorable);
            destroyUI.label = "Destroy item on enter region";

            this._cumulativeStorable = new JSONStorableBool("cumulative", false);
            this._script.RegisterBool(this._cumulativeStorable);
            UIDynamicToggle cumulativeUI = this._script.CreateToggle(this._cumulativeStorable);
            cumulativeUI.label = "Treat the values as increment per second, not increment per collision";
            cumulativeUI.height = 100;


            /*this._destroyStorable.setCallbackFunction += (val) => {
                acumulativeUI.enabled = !val; // if it gets destroyed (this._destroyStorable == true) then it can't be acumulative
                this._acumulativeStorable.val = false; // set the default value
            };*/

            UIDynamicButton addLiteralBtn = this._script.CreateButton("Add literal object");
            addLiteralBtn.button.onClick.AddListener(() => this.AddLiteralCollider());

            UIDynamicButton addRegexBtn = this._script.CreateButton("Add regex object");
            addRegexBtn.button.onClick.AddListener(() => this.AddRegexCollider());
            
            this._durationStorable = new JSONStorableFloat("duration", 10.0f, (float val) => this._ti.AnimationDurationChanged(val), 0.1f, 120.0f);
            this._script.RegisterFloat(this._durationStorable);
            this._script.CreateSlider(this._durationStorable, true); // create on the right

            this._offsetDownStorable = new JSONStorableFloat("offset_down", 0.05f, (float val) => this._ti.OffsetChanged(val, this._offsetFrontStorable.val), -2f, 2f);
            this._script.RegisterFloat(this._offsetDownStorable);
            this._script.CreateSlider(this._offsetDownStorable, true);
            this._offsetFrontStorable = new JSONStorableFloat("offset_front", 0f, (float val) => this._ti.OffsetChanged(this._offsetFrontStorable.val, val), -2f, 2f);
            this._script.RegisterFloat(this._offsetFrontStorable);
            this._script.CreateSlider(this._offsetFrontStorable, true);

            JSONStorableStringChooser morphChooseList = new JSONStorableStringChooser("morph", null, "", "Add morph", (string morphName) => {
                SuperController.LogMessage("Added " + morphName);

                this.AddMorphSlider(morphName);
                this._ti.AddMorph(morphName);
            });
            // we don't care about this information
            var linkPopup = this._script.CreateFilterablePopup(morphChooseList, true);
            linkPopup.popupPanelHeight = 600f;
            linkPopup.popup.onOpenPopupHandlers += () => { morphChooseList.choices = this.GetUnusedMorphNames(); };
        }

        public void Update() {
            this._events.Update();
        }

        public void OnAtomRename(string oldid, string newid) {
            this._events.SyncAtomNames();
        }

        public void OnDestroy() {
            this._events.Remove();
        }

        public JSONClass UpdateJSON(bool includePhysical, bool includeAppearance, bool forceStore, JSONClass jc) {
            if (includePhysical || forceStore) jc[this._events.Name] = this._events.GetJSON(this._script.subScenePrefix);
            return jc;
        }

        public Action GetTriggerInvoker() {
            return () => this._events.Trigger();
        }

        public void LateRestoreFromJSON(JSONClass jc, bool restorePhysical, bool restoreAppearance, bool setMissingToDefault) {
            if (!this._script.physicalLocked && restorePhysical) {
                this._events.Remove();
                this._events.RestoreFromJSON(jc, this._script.subScenePrefix, this._script.mergeRestore, setMissingToDefault);
            }
        }

        public void AddLiteralCollider(string name = "") {
            JSONStorableStringChooser colliderStorable = new JSONStorableStringChooser("collider_literal_" + this._colliderSelectors.Count().ToString(),
                        null, "", "Collider", (string colliderName) => this.ColliderStorableChanged());
            this._script.RegisterStringChooser(colliderStorable);
            var literalLinkPopup = this._script.CreateFilterablePopup(colliderStorable);
            literalLinkPopup.popupPanelHeight = 600f;
            literalLinkPopup.popup.onOpenPopupHandlers += () => { colliderStorable.choices = SuperController.singleton.GetAtoms().Select(a => a.name).Distinct().ToList(); }; // TODO subtract already added
            
            this._colliderSelectors.Add(colliderStorable);

            colliderStorable.val = name;
        }

        public void AddRegexCollider(string name = "") {
            JSONStorableString colliderStorable = new JSONStorableString("collider_regex_" + this._colliderSelectors.Count().ToString(),
                        "", (string newValue) => this.ColliderStorableChanged());
            this._script.RegisterString(colliderStorable);
            TriggerIncrementerUI.CreateTextInput(this._script, colliderStorable);

            this._colliderSelectors.Add(colliderStorable);

            colliderStorable.val = name;
        }
        
        public void LoadJson(JSONNode node) {
            if (node == null) return;

            foreach (string entry in node.AsObject.Keys) {
                switch (entry) {
                    case "collider":
                    case "duration":
                    case "OnCollide":
                    case "destroy":
                    case "cumulative":
                    case "offset_down":
                    case "offset_front":
                        break; // automatically loaded

                    case "run": // deprecated
                        break;

                    case "id":
                    case "pluginLabel":
                        break; // ignore

                    default:
                        if (entry.StartsWith("collider_literal_")) {
                            this.AddLiteralCollider(node[entry]);
                        }
                        else if (entry.StartsWith("collider_regex_")) {
                            this.AddRegexCollider(node[entry]);
                        }
                        else {
                            // tracked morphs
                            try {
                                this.AddMorphSlider(entry, node[entry].AsFloat);
                                this._ti.AddMorph(entry);
                            } catch (Exception ex) { }
                        }
                        break;
                }
            }

            this._ti.AnimationDurationChanged(this._durationStorable.val);
            this.ColliderStorableChanged();
        }

        private void ColliderStorableChanged() {
            List<SceneObject> colliders = new List<SceneObject>();
            foreach (JSONStorableParam storable in this._colliderSelectors) {
                if (storable.GetType().Equals(typeof(JSONStorableStringChooser))) {
                    // Literal
                    string value = ((JSONStorableStringChooser)storable).val;
                    if (!value.Equals("")) colliders.Add(new ConcreteSceneObject(value));
                }
                else if (storable.GetType().Equals(typeof(JSONStorableString))) {
                    // RegEx
                    string value = ((JSONStorableString)storable).val;
                    if (!value.Equals("")) {
                        try {
                            colliders.Add(new MultipleSceneObject(value));
                        } catch (Exception ex) {
                            SuperController.LogError("Error while parsing RegEx '" + value + "': " + ex.Message);
                        }
                    }
                }
                else SuperController.LogError("Unknown class " + storable.GetType());
            }

            this._ti.SetTargetObjects(colliders);
        }

        public void AddMorphSlider(string morphName, float value = 0.0f) {
            JSONStorableFloat jsonFloat = new JSONStorableFloat(morphName, 0.0f, -1.0f, 1.0f);
            jsonFloat.val = value;
            this._script.RegisterFloat(jsonFloat);
            this._script.CreateSlider(jsonFloat, true);

            this._morphIncrement.Add(morphName, jsonFloat);
        }

        public float GetMorphIncrement(string morphName) {
            try {
                return this._morphIncrement[morphName].val;
            } catch (KeyNotFoundException) {
                return 0.0f; // no morph => no increment
            }
        }
        
        public List<string> GetSelectedMorphNames() {
            return this._morphIncrement.Keys.ToList();
        }

        public bool GetRemoveOnCollision() {
            return this._destroyStorable.val;
        }

        public bool GetCumulativeCollision() {
            return this._cumulativeStorable.val;
        }

        private List<string> GetUnusedMorphNames() {
            List<string> r = this._ti.GetAllMorphNames(),
                        alreadySelected = this.GetSelectedMorphNames();
            r.RemoveAll(s => alreadySelected.Contains(s));
            return r;
        }
    }
}
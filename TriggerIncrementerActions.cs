using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JustAnotherUser {
    abstract public class SceneObject {
        public override abstract bool Equals(Object obj);
    }

    public class ConcreteSceneObject : SceneObject {
        private string _name;

        public ConcreteSceneObject(string name) {
            this._name = name;
        }

        public override bool Equals(Object obj) {
            if ((obj == null) || !obj.GetType().Equals(typeof(string))) return false;
            return this._name.Equals((string)obj);
        }
    }

    public class MultipleSceneObject : SceneObject {
        private Regex _regex;

        public MultipleSceneObject(string regex) {
            this._regex = new Regex(regex);
        }

        public override bool Equals(Object obj) {
            if ((obj == null) || !obj.GetType().Equals(typeof(string))) return false;
            return this._regex.IsMatch((string)obj);
        }
    }

    public interface TriggerIncrementerActions {
        // add a morph action based on the name
        void AddMorph(string morphName);

        // get the name of all the aplicable morphs
        List<string> GetAllMorphNames();

        // duration changed
        void AnimationDurationChanged(float newDuration);

        // collider offset changed
        void OffsetChanged(float down, float front);

        // sets the collider that will trigger the animation
        void SetTargetObjects(List<SceneObject> objects);
    }
}
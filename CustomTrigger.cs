using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JustAnotherUser {
    public interface TriggerAction {
        // the desired item colided
        void OnCollision(Atom a);
        
        // one of the desired items stop being colliding
        void OnRelease(Atom a);

        // get one unique identifier
        string GetUUID();

        // is an atom desired?
        bool IsDesired(Atom a);

        void RunCoroutine(IEnumerator enumerator);
    }

    public interface CustomTrigger {
        void SetCallback(TriggerAction callback);

        void SetFocus(Component focus, Vector3 offset, float size);

        void SetFocus(Vector3 offset);

        void Update();
    }
}

using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class AIActionMoveInDirection2D : AIAction {
	[SerializeField] protected Vector2 direction;
	protected CharacterMovement _characterMovement;

    public override void Initialization() {
			if(!ShouldInitialize) return;
			base.Initialization();
			_characterMovement = GetComponentInParent<Character>()?.FindAbility<CharacterMovement>();
	}

    public override void PerformAction() {
        _characterMovement?.SetMovement(direction);
    }
    
    public override void OnExitState() {
        base.OnExitState();

        _characterMovement?.SetHorizontalMovement(0f);
        _characterMovement?.SetVerticalMovement(0f);
    }
}

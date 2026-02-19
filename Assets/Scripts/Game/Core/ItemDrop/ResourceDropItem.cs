
public class ResourceDropItem : DropItem<ResourceType> {
    public override void Pick() {
        EarnResourceEvent.Trigger(EventStage.Process, data, ResourceSource.Gameplay, name, (int)amount);
        base.Pick();
    }
}

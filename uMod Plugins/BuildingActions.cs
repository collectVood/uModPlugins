namespace Oxide.Plugins
{
    [Info("Building Actions", "Iv Misticos", "1.0.1")]
    [Description("Rotate and demolish buildings when you want!")]
    class BuildingActions : RustPlugin
    {
        private void OnServerInitialized()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BuildingBlock>())
            {
                OnEntitySpawned(entity);
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var block = entity as BuildingBlock;
            if (block == null)
                return;
            
            block.CancelInvoke(block.StopBeingDemolishable);
            block.CancelInvoke(block.StopBeingRotatable);
            block.SetFlag(BaseEntity.Flags.Reserved1, true);
            block.SetFlag(BaseEntity.Flags.Reserved2, true);
        }
    }
}
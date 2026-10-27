namespace Game.Character
{
    /// <summary>
    /// 【新增：联机第六步】表现层读取运动结果的通用接口，不包含 NGO 类型。
    /// 单机由 CharacterMotor 提供；联网由 NetworkCharacter 提供服务器复制的状态。
    /// CharacterView 不需要知道状态是本地计算还是网络同步得到的。
    /// </summary>
    public interface IMovementStateSource
    {
        MovementState CurrentMovementState { get; }
    }
}

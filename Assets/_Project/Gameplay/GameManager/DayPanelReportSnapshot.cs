/// <summary>Immutable presentation values. These snapshots never settle money or change campaign state.</summary>
public readonly struct DayPanelIntroSnapshot
{
    public readonly string Title, Hours;
    public readonly int TargetRevenue, Capacity, ArrivalRate;
    public readonly float RealSecondsPerGameHour;

    public DayPanelIntroSnapshot(string title, string hours, int targetRevenue, int capacity,
        int arrivalRate, float realSecondsPerGameHour)
    {
        Title = title;
        Hours = hours;
        TargetRevenue = targetRevenue;
        Capacity = capacity;
        ArrivalRate = arrivalRate;
        RealSecondsPerGameHour = realSecondsPerGameHour;
    }
}

/// <summary>One completed report's real values, captured after the existing settlement/reward flow.</summary>
public sealed class DayPanelReportSnapshot
{
    public readonly string Title, Status, Coaching, Notice, OutcomeReason, ObjectiveGrade;
    public readonly int Day, Revenue, TargetRevenue, Tips, CashErrors, OrdersProcessed, FoodServed,
        Happy, Neutral, Angry, Approval, ReservationsBooked, ReservationsOnTime, ReservationsUnserved, EarnedStars;
    public readonly bool HasObjectiveGrade, HasReservations, IsOutcome, IsVictory;

    public DayPanelReportSnapshot(string title, string status, string coaching, string notice,
        string outcomeReason, string objectiveGrade, int day, int revenue, int targetRevenue,
        int tips, int cashErrors, int ordersProcessed, int foodServed, int happy, int neutral,
        int angry, int approval, int reservationsBooked, int reservationsOnTime, int reservationsUnserved,
        int earnedStars, bool hasObjectiveGrade, bool hasReservations, bool isOutcome, bool isVictory)
    {
        Title = title;
        Status = status;
        Coaching = coaching;
        Notice = notice;
        OutcomeReason = outcomeReason;
        ObjectiveGrade = objectiveGrade;
        Day = day;
        Revenue = revenue;
        TargetRevenue = targetRevenue;
        Tips = tips;
        CashErrors = cashErrors;
        OrdersProcessed = ordersProcessed;
        FoodServed = foodServed;
        Happy = happy;
        Neutral = neutral;
        Angry = angry;
        Approval = approval;
        ReservationsBooked = reservationsBooked;
        ReservationsOnTime = reservationsOnTime;
        ReservationsUnserved = reservationsUnserved;
        EarnedStars = earnedStars;
        HasObjectiveGrade = hasObjectiveGrade;
        HasReservations = hasReservations;
        IsOutcome = isOutcome;
        IsVictory = isVictory;
    }
}

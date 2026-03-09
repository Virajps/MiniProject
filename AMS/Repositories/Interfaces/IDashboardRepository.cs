public interface IDashboardRepository
{
    DashboardModel GetDashboardData();
    public Task<List<AccessModel>> GetAllUsersForAccess();
}
using DalApi;

namespace Helpers;

internal static class OrderManager
{
    private static IDal s_dal = Factory.Get;
    
    internal static BO.Order? GetOrder(int id)
    {
        DO.Order doOrder;
        doOrder = s_dal.Order.Read(id) ?? throw new BO.BlDoesNotExistException($"Order with ID {id} does not exist.");

        return new BO.Order
        {
            Id = doOrder.Id,
            CustomerName = doOrder.CustomerName,
            CustomerAddress = doOrder.CustomerAddress,
            CustomerPhone = doOrder.CustomerPhone,
            OrderDate = doOrder.OrderDate,
            OrderDescription = doOrder.Description,
            Latitude = doOrder.Latitude ?? 0,
            Longitude = doOrder.Longitude ?? 0,
            Fragility = (BO.FragilityLevel?)doOrder.Fragility,
        };
    }
    internal static BO.StudentInCourse GetDetailedCourseForStudent(int studentId, int courseId)
    {
        DO.Link? doLink = s_dal.Link.Read(l => l.StudentId == studentId && l.CourseId == courseId)
            ?? throw new BO.BlDoesNotExistException($"Student with ID={studentId} does Not take Course with ID={courseId}");
        DO.Course? doCourse = s_dal.Course.Read(courseId)
     ?? throw new BO.BlDoesNotExistException($"Course with ID={courseId} does Not exist");

        return new BO.StudentInCourse()
        {
            StudentId = studentId,
            Course = new Tuple<int, string, string>(doCourse.Id, doCourse.CourseNumber, doCourse.CourseName),
            InYear = (BO.Year?)doCourse.InYear,
            InSemester = (BO.SemesterNames?)doCourse.InSemester,
            Grade = doLink.Grade,
            Credits = doCourse.Credits
        };


    }

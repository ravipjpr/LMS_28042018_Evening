using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CLSLms;
using LMS.Models;
using System.IO;
using System.Xml;
using CLSScorm.LeopinkLMS;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Data.SqlClient;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace LMS.Controllers
{
    [CustomAuthorize]
    public class UserController : Controller
    {
        private LeopinkLMSDBEntities db = new LeopinkLMSDBEntities();

        public UserController()
            : this(new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext())))
        {
            UserManager.UserValidator = new UserValidator<ApplicationUser>(UserManager) { AllowOnlyAlphanumericUserNames = false };
        }

        public UserController(UserManager<ApplicationUser> userManager)
        {
            UserManager = userManager;
        }

        public UserManager<ApplicationUser> UserManager { get; private set; }

        // GET: UserCourse

        public ActionResult Index(int id = 0)
        {
            int GroupID = 0;
            int UserID;
            int languageId = 0;
            UserID = Convert.ToInt32(Session["UserID"].ToString());
            languageId = int.Parse(Session["LanguageId"].ToString());
            Session["IsAdminView"] = "false";
            //get the default groupid of the currently logged in user
            if (id == 0)
                GroupID = (from UGrp in db.UserGroups
                           where UGrp.UserId == UserID
                           orderby UGrp.UserGroupId descending
                           select UGrp.GroupID).FirstOrDefault();
            else
                GroupID = (from UGrp in db.UserGroups
                           where UGrp.UserId == UserID && UGrp.GroupID == id
                           select UGrp.GroupID).FirstOrDefault();

            //ViewBag.GroupId = GroupID;

            //var result = db.GetHomePage(GroupID, languageId).ToList();
            ViewBag.GroupName = db.Groups.Find(GroupID).GroupName.ToString();
            var resultUserCourse = db.GetUserCourse(Convert.ToInt64(Session["UserID"].ToString()), GroupID, languageId).ToList();

            ViewBag.totalNotStarted = resultUserCourse.Where(x => x.Status.Equals("Not started")).Count();
            ViewBag.totalInProgress = resultUserCourse.Where(x => x.Status.Equals("In progress")).Count();
            ViewBag.totalCompleted = resultUserCourse.Where(x => x.Status.Equals("Completed")).Count() + resultUserCourse.Where(x => x.Status.Equals("Pass")).Count();
            //return View(result);
            return View();
        }

        public ActionResult AjaxHandlerUserCourse(jQueryDataTableParamModel param)
        {
            IEnumerable<UserCourse> filterCourse = null;
            long userID = Convert.ToInt64(Session["UserID"].ToString());
            UserProfile UserDetail = db.UserProfiles.Where(a => a.UserId == userID).FirstOrDefault();
            /// search action
            if (!string.IsNullOrEmpty(param.sSearch))
            {
                filterCourse = from UserCour in db.UserCourses
                               where UserCour.Course.CourseName.ToLower().Contains(param.sSearch.ToLower())
                               select UserCour;

            }
            else
            {
                filterCourse = from UserCour in db.UserCourses
                               orderby UserCour.Course.CourseName
                               select UserCour;
            }

            filterCourse = filterCourse.Where(UserCour => UserCour.UserId == Convert.ToInt64(Session["UserID"].ToString())).ToList();

            // records to display            
            var displayedCourse = filterCourse.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedCourse = filterCourse;

            int languageId = 0;
            languageId = int.Parse(Session["LanguageId"].ToString());
            if (UserDetail != null && !UserDetail.Organisation.IsUserAssignment)
            {
                var result1 = (from obj in db.GetUserCourse(Convert.ToInt64(Session["UserID"].ToString()), Convert.ToInt32(param.fCol1), languageId).ToList()
                               select new[] {
                               (obj.CourseName==null?"":obj.CourseName),
                                Convert.ToString(obj.ContentType),
                               obj.Status,
                               obj.ExpiryDate==null? "":string.Format("{0:dd/MM/yyyy}",obj.ExpiryDate),
                               obj.LastAccessDate==null?"":string.Format("{0:dd/MM/yyyy HH:mm}",obj.LastAccessDate),
                               Convert.ToString(obj.CourseId),
                               (obj.CourseFile==null)?"":obj.CourseFile,
                               Convert.ToString(obj.WindowHeight),
                               Convert.ToString(obj.WindowWidth),
                               Convert.ToString(obj.IsAccessible),
                               Convert.ToString(obj.ContentType),
                               Convert.ToString(obj.CourseType),
                               Session["EmployeeID"].ToString(),
                               System.Configuration.ConfigurationManager.AppSettings["xapiEndpoint"].ToString()
                          }).ToList();
                var result2 = (from obj in db.GetUserDocument(Convert.ToInt64(Session["UserID"].ToString()), Convert.ToInt32(param.fCol1), languageId).ToList()
                               select new[] {
                               (obj.CourseName==null?"":obj.CourseName),
                                Convert.ToString(obj.ContentType),
                               obj.LastAccessDate==null?"Not viewed":"Viewed",
                               obj.ExpiryDate==null? "":string.Format("{0:dd/MM/yyyy}",obj.ExpiryDate),
                               obj.LastAccessDate==null?"":string.Format("{0:dd/MM/yyyy HH:mm}",obj.LastAccessDate),
                               Convert.ToString(obj.CourseId),
                               (obj.CourseFile==null)?"":obj.CourseFile,
                               Convert.ToString(obj.WindowHeight),
                               Convert.ToString(obj.WindowWidth),
                               Convert.ToString(obj.IsAccessible)

                          }).ToList();

                //Currently groupid is hard coded
                var result = result1.Union(result2).ToList().OrderBy(a => a.ElementAt(0));


                return Json(new
                {
                    sEcho = param.sEcho,
                    iTotalRecords = filterCourse.Count(),
                    iTotalDisplayRecords = filterCourse.Count(),
                    aaData = result
                },
                JsonRequestBehavior.AllowGet);

            }
            else
            {
                var result = from obj in db.GetUserAllCourses(Convert.ToInt64(Session["UserID"].ToString()), languageId).ToList().Where(a => a.UserStatus != "0").ToList()

                             select new[] {
                               (obj.CourseName==null?"":obj.CourseName),
                                Convert.ToString(obj.ContentType),
                               obj.Status,
                               obj.ExpiryDate==null? "":string.Format("{0:dd/MM/yyyy}",obj.ExpiryDate),
                               obj.LastAccessDate==null?"":string.Format("{0:dd/MM/yyyy HH:mm}",obj.LastAccessDate),
                               Convert.ToString(obj.CourseId),
                               (obj.CourseFile==null)?"":obj.CourseFile,
                               Convert.ToString(obj.WindowHeight),
                               Convert.ToString(obj.WindowWidth),
                               Convert.ToString(obj.IsAccessible)

                          };

                return Json(new
                {
                    sEcho = param.sEcho,
                    iTotalRecords = filterCourse.Count(),
                    iTotalDisplayRecords = filterCourse.Count(),
                    aaData = result
                },
                JsonRequestBehavior.AllowGet);
            }
        }        

        #region // Edit User
        public ActionResult EditUserProfile()
        {
            long UserId;
            UserId = Convert.ToInt64(Session["UserID"].ToString());

            UserProfileModel us = new UserProfileModel();
            var UserRoles = new UserRolesLocalView();

            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            //var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            // find the user in userprofile table
            var userExist = db.UserProfiles.Find(UserId);
            if (userExist.IsDelete == false) // record is not deleted.
            {
                // update and save the record.
                us.UserId = userExist.UserId;
                us.ID = userExist.Id;
                us.EmployeeID = userExist.EmployeeID;
                us.FirstName = userExist.FirstName;
                us.LastName = userExist.LastName;
                us.EmailAddress = userExist.EmailAddress;
                us.ContactNo = userExist.ContactNo;
                us.ManagerName = userExist.ManagerName;
                us.Designation = userExist.Designation;
                us.OrganisationID = (int)userExist.OrganisationID;
                //us.OrganisationName = String.IsNullOrWhiteSpace(userExist.Organisation.OrganisationName) ? "" : userExist.Organisation.OrganisationName;
                us.Status = userExist.Status;
                us.Option1 = userExist.Option1;
                us.Option2 = userExist.Option2;
                us.UserLanguageId = Convert.ToInt32(userExist.LanguageId);
                us.IsGroupAdmin = IsGroupAdmin;
            }
            
            return View(us);

        }

        [HttpPost]
        public ActionResult EditUserProfile(UserProfileModel userObj)
        {
            var objUser = db.UserProfiles.Find(userObj.UserId);
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            if (objUser == null)
            {
                return HttpNotFound();
            }
            else
            {
                if (ModelState.IsValid)
                {
                    //string[] ia = { };
                    //if (userObj.UserRolesList != null)
                    //    if (userObj.UserRolesList.PostedRoles != null)
                    //        if (userObj.UserRolesList.PostedRoles.UserRolesLocalIds.Length > 0)
                    //            ia = userObj.UserRolesList.PostedRoles.UserRolesLocalIds;

                    #region // edit users profile fields

                    //objUser.EmployeeID = userObj.EmployeeID;
                    objUser.FirstName = userObj.FirstName;
                    objUser.LastName = userObj.LastName;
                    objUser.ContactNo = userObj.ContactNo;
                    //objUser.ManagerName = userObj.ManagerName;                    
                    objUser.DateLastModified = DateTime.Now;
                    objUser.LastModifiedByID = Convert.ToInt64(Session["UserID"].ToString());

                    if (string.IsNullOrEmpty(userObj.Password) == false)
                    {
                        UserManager.RemovePassword(objUser.Id);
                        UserManager.AddPassword(objUser.Id, userObj.Password);
                    }
                    db.SaveChanges();

                    #endregion
                    if (Session["IsParentTeacher"].ToString().ToLower() == "true")
                    {
                        return RedirectToAction("Index", "ParentTeacher");
                    }
                    else
                    {
                        return RedirectToAction("Index", "User");
                    }
                }

            }
            return View(userObj);

        }
        #endregion        
    }
}
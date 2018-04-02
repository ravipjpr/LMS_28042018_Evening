using CLSLms;
using LMS.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace LMS.Controllers
{
    [CustomAuthorize]
    public class UserManagementController : Controller
    {
        private LeopinkLMSDBEntities db = new LeopinkLMSDBEntities();

        public UserManagementController()
            : this(new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext())))
        {
            UserManager.UserValidator = new UserValidator<ApplicationUser>(UserManager) { AllowOnlyAlphanumericUserNames = false };
        }

        public UserManagementController(UserManager<ApplicationUser> userManager)
        {
            UserManager = userManager;
        }

        public UserManager<ApplicationUser> UserManager { get; private set; }



        #region // User Listing
        public ActionResult Index()
        {
            return View();

        }

        public ActionResult AjaxHandlerUserListing(jQueryDataTableParamModel param)
        {
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var sortColumnIndex = Convert.ToInt32(Request["iSortCol_0"]);
            Func<UserProfile, string> orderingFunction = (c => sortColumnIndex == 0 ? string.IsNullOrEmpty(c.EmployeeID) ? "-" : c.EmployeeID.ToLower() :
                                                        sortColumnIndex == 1 ? c.FirstName.ToLower() :
                                                        sortColumnIndex == 2 ? string.IsNullOrWhiteSpace(c.LastName) ? "-" : (c.LastName).ToLower() :
                                                        sortColumnIndex == 3 ? c.EmailAddress.ToLower() :
                                                        sortColumnIndex == 4 ? c.Organisation.OrganisationName.ToLower() :
                                                        sortColumnIndex == 7 ? c.Status.ToString() :
                                                        c.EmailAddress.ToLower());
            var sortDirection = Request["sSortDir_0"];
            IEnumerable<UserProfile> filterUserProfile = null;


            // if current user is group admin then the admin can view only those user which are in associeated to the admin groups. and user is not a administrator.
            var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentLoginUser).Select(x => x.GroupID).ToList();
            var GroupAdminUsers = db.UserGroups.Where(x => GroupAdminGroups.Contains(x.GroupID) && x.UserProfile.AspNetUser.AspNetUserRoles.Select(y => y.AspNetRole.Name).Contains("Administrator") == false).Select(x => x.UserId).Distinct().ToArray();



            /// search action
            if (!string.IsNullOrEmpty(param.sSearch))
            {
                if ((Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2))
                {

                    filterUserProfile = from or in db.UserProfiles
                                        join ur in db.AspNetUserRoles on or.Id equals ur.UserId
                                        join r in db.AspNetRoles on ur.RoleId equals r.Id
                                        join ug in db.UserGroups on or.UserId equals ug.UserId
                                        join g in db.Groups on ug.GroupID equals g.GroupID
                                        where or.EmployeeID.ToLower().Contains(param.sSearch.ToLower()) ||
                                        or.FirstName.ToLower().Contains(param.sSearch.ToLower()) ||
                                        or.LastName.ToLower().Contains(param.sSearch.ToLower()) ||
                                        or.EmailAddress.ToLower().Contains(param.sSearch.ToLower()) ||
                                        or.Organisation.OrganisationName.ToLower().Contains(param.sSearch.ToLower()) ||
                                        r.Name.ToLower().Contains(param.sSearch.ToLower()) ||
                                        g.GroupName.ToLower().Contains(param.sSearch.ToLower())
                                        where GroupAdminUsers.Contains(or.UserId) && or.IsDelete == false
                                        select or;
                }
                else
                {
                    filterUserProfile = from or in db.UserProfiles
                                        join ur in db.AspNetUserRoles on or.Id equals ur.UserId
                                        join r in db.AspNetRoles on ur.RoleId equals r.Id
                                        join ug in db.UserGroups on or.UserId equals ug.UserId
                                        join g in db.Groups on ug.GroupID equals g.GroupID
                                        where or.EmployeeID.ToLower().Contains(param.sSearch.ToLower()) ||
                                        or.FirstName.ToLower().Contains(param.sSearch.ToLower()) ||
                                        or.LastName.ToLower().Contains(param.sSearch.ToLower()) ||
                                        or.EmailAddress.ToLower().Contains(param.sSearch.ToLower()) ||
                                        or.Organisation.OrganisationName.ToLower().Contains(param.sSearch.ToLower()) ||
                                        r.Name.ToLower().Contains(param.sSearch.ToLower()) ||
                                        g.GroupName.ToLower().Contains(param.sSearch.ToLower())
                                        where or.IsDelete == false
                                        select or;
                }


            }
            else
            {
                if ((Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2))
                {
                    filterUserProfile = from or in db.UserProfiles
                                        where GroupAdminUsers.Contains(or.UserId) && or.IsDelete == false
                                        select or;
                }
                else
                {
                    filterUserProfile = from or in db.UserProfiles
                                        where or.IsDelete == false
                                        select or;
                }
            }

            // ordering action
            if (sortDirection == "asc")
            {
                filterUserProfile = filterUserProfile.OrderBy(orderingFunction);
            }
            else if (sortDirection == "desc")
            {
                filterUserProfile = filterUserProfile.OrderByDescending(orderingFunction);
            }

            //filterUserProfile = filterUserProfile.Where(x=>x.IsDelete ==false).ToList();

            // records to display            
            var displayedUserProfile = filterUserProfile.Skip(param.iDisplayStart).Take(param.iDisplayLength);
            if (param.iDisplayLength == -1)
                displayedUserProfile = filterUserProfile;
            var ActiveStatus = LMSResourse.Common.Common.lblActiveStatus;
            var InactiveStatus = LMSResourse.Common.Common.lblInactiveStatus;
            var result = from obj in displayedUserProfile.Distinct()
                         select new[] {
                              string.IsNullOrWhiteSpace(obj.EmployeeID)?"-":obj.EmployeeID,
                              obj.FirstName,
                              string.IsNullOrWhiteSpace(obj.LastName)?"-":obj.LastName,
                              obj.EmailAddress,
                              obj.OrganisationID == null?"-":obj.Organisation.OrganisationName,
                              Common.GetString(UserManager.GetRoles(obj.Id).ToArray()),
                              (obj.UserGroups.Count()>0)?JsonConvert.SerializeObject(obj.UserGroups.DefaultIfEmpty().Select(x=>new string[]{x.Group.GroupName}).ToArray()).Replace("[[\"","").Replace("\"]]","").Replace("\"],",", ").Replace("[\"",""):"",
                              ((obj.Status)?ActiveStatus :InactiveStatus ),
                              //string.Format("{0:dd/MM/yyyy HH:mm}",obj.DateLastModified),
                              Convert.ToString(obj.UserId),
                             ((obj.Organisation==null)?"False":Convert.ToString(obj.Organisation.IsUserAssignment))
                          };

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = filterUserProfile.Count(),
                iTotalDisplayRecords = filterUserProfile.Count(),
                aaData = result
            },
                           JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region // Get the optional user profile values with there validation messages
        public ActionResult GetUserProfiles(int OrgId, int LangaugeId = 0)
        {
            if (LangaugeId == 0) LangaugeId = db.InstanceInfoes.Find(1).DefaultLanguage;
            List<UserProfilesOption_L> Obj = new List<UserProfilesOption_L>();
            var x = from usOrg in db.UserProfileSettingsOrgs
                    join usInfo in db.UserProfileSettingsInfoes on usOrg.ProfileSettignOrgId equals usInfo.ProfileSettignOrgId
                    where usOrg.OrganisationID == OrgId && usInfo.LanguageID == LangaugeId
                    orderby usOrg.ProfileSettingID
                    select new UserProfilesOption_L
                    {
                        ProfileSettignOrgId = usOrg.ProfileSettignOrgId,
                        profileSettingId = usOrg.ProfileSettingID,
                        ProfileTitle = usInfo.ProfileTitle,
                        IsDisplay = (usOrg.IsDisplay == null) ? false : (bool)usOrg.IsDisplay,
                        IsMandatory = (usOrg.IsMandatory == null) ? false : (bool)usOrg.IsMandatory,
                        ProfileType = (usOrg.ProfileType == null) ? 0 : (int)usOrg.ProfileType,
                        MaxLength = usOrg.MaxLength,
                        ReqValidationMessage_For_Text = LMSResourse.Admin.User.msgReqOption_n_text + " " + usInfo.ProfileTitle,
                        ReqValidationMessage_For_Dropdown = LMSResourse.Admin.User.msgReqOption_n_dropdown + " " + usInfo.ProfileTitle
                    };
            Obj = x.ToList();
            foreach (var i in Obj)
            {
                if (i.ProfileType == 2)
                {
                    var proOrgValues = from UspOrgVal in db.UserProfileSettingsOrgValues
                                       where UspOrgVal.ProfileSettignOrgId == i.ProfileSettignOrgId
                                       select new UserProfilesOptionValues_L { ValueId = UspOrgVal.UserProfileSettingsOrgValuesId, ValueText = UspOrgVal.ProfileValuesTitle };
                    i.DropdownValues = proOrgValues.ToList();
                }
            }


            return Json(Obj, JsonRequestBehavior.AllowGet);
            //return Obj;
        }
        #endregion

        #region // Create User
        [CustomAuthorize]
        public ActionResult CreateUser()
        {
            UserModel us = new UserModel();
            var UserRoles = new UserRolesLocalView();
            var UserGroup = new UserGroupsLocalView();

            var instinfo = db.InstanceInfoes.Find(1);
            us.ActionType = 0;
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"])); // user logs in

            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == 0).Select(x => x.GroupID).ToList();

            var selectedRoleLocal = db.AspNetRoles.Where(rol => rol.Name == instinfo.RoleName).Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = true }).OrderByDescending(rol => rol.RoleName).ToList();
            UserRoles.AvailableRoles = db.AspNetRoles.Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = false }).ToList();
            if (IsGroupAdmin)
            {
                GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentLoginUser).Select(x => x.GroupID).ToList();
                UserRoles.AvailableRoles = db.AspNetRoles.Where(x => x.Name != "Administrator").Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = false }).ToList();
            }
            UserRoles.SelectedRoles = selectedRoleLocal;
            us.UserRolesList = UserRoles;

            var selectedGroupLocal = db.Groups.Where(g => g.GroupID == 0).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID.ToString(), GroupName = g.GroupName, IsSelected = true }).OrderBy(g => g.GroupName).ToList();
            if (currentuser != null)
            {

                us.IsGroupAdmin = IsGroupAdmin;
                // Group that is not associeated to any organisation will not be listed in group list.
                UserGroup.AvailableGroups = db.Groups.Where(g => g.IsDeleted == false && g.OrganisationID != null && g.Status == true && (IsGroupAdmin ? GroupAdminGroups.Contains(g.GroupID) : 0 == 0)).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID, GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();


                //if (UserManager.IsInRole(currentuser.Id,db.AspNetRoles.Find(db.InstanceInfoes.Find(1).GroupAdminId).Name.ToString()))
                //{
                //  us.IsGroupAdmin = true;
                //  UserGroup.AvailableGroups = db.Groups.Where(g => g.OrganisationID == currentuser.OrganisationID).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID, GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
                //}
                //else
                //{
                //    UserGroup.AvailableGroups = db.Groups.Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + (g.OrganisationID == null ? "0" : g.OrganisationID.ToString()), GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
                //}
                UserGroup.SelectedGroups = selectedGroupLocal;
                us.UserGroupList = UserGroup;
            }
            if (us.IsGroupAdmin == true)
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true && org.ExpiryDate >= DateTime.Now && org.OrganisationID == currentuser.OrganisationID && org.IsDeleted == false).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true && org.ExpiryDate >= DateTime.Now && org.IsDeleted == false).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            us.Status = true;

            ViewBag.DistrictList = new SelectList(db.Districts.OrderBy(d => d.DistrictName).Select(d => d), "DistrictId", "DistrictName");
            ViewBag.BlockList = new SelectList(new List<DistrictBlock>(), "BlockId", "BlockName");
            ViewBag.SchoolList = new SelectList(new List<DistrictSchool>(), "SchoolId", "SchoolName");
            return View(us);
        }
        /// <summary>
        /// http post method to create user 
        /// </summary>
        /// <param name="userObj"></param>
        /// <returns></returns>
        [HttpPost]
        [CustomAuthorize]
        public ActionResult CreateUser(UserModel userObj)
        {
            if (ModelState.IsValid)
            {
                #region // Checking Max user allowed for the current Group

                Boolean wiilAdd = true;
                if (userObj.UserGroupList != null)
                    foreach (var grp in userObj.UserGroupList.PostedGroups.UserGroupsLocalIds)
                    {
                        var tempGroupId = Convert.ToInt32(grp.ToString().Split('~')[0].ToString());
                        if (tempGroupId > 0)
                        {
                            var groupDetail = db.Groups.Where(a => a.GroupID == tempGroupId).FirstOrDefault();
                            if (groupDetail.MaxUsers > groupDetail.UserGroups.Count())
                            { }
                            else
                            { wiilAdd = false; }
                        }
                    }
                #endregion

                if (wiilAdd == true)
                {
                    string[] ia = { };
                    if (userObj.UserRolesList != null)
                        if (userObj.UserRolesList.PostedRoles != null)
                            if (userObj.UserRolesList.PostedRoles.UserRolesLocalIds.Length > 0)
                                ia = userObj.UserRolesList.PostedRoles.UserRolesLocalIds;

                    #region // user creation

                    var user = new ApplicationUser() { UserName = userObj.EmailAddress }; //  create a object of application user
                    userObj.Password = string.IsNullOrEmpty(userObj.Password) ? AjaxGeneratePassword() : userObj.Password; // if password is not provided at the time of user creation then generate the password.
                    var result = UserManager.Create(user, userObj.Password.ToString()); // create a user by usermanager by passing username(i.e email address) and password. this will create record in aspNetUsers table.
                    #endregion

                    if (result.Succeeded) // check the status of user creation.
                    {
                        #region // Save User Detail

                        var objUser = new UserProfile(); // create the object of userprofile in which orther information of user is saved 
                        objUser.Id = user.Id;
                        objUser.EmployeeID = userObj.EmployeeID;
                        objUser.FirstName = userObj.FirstName;
                        objUser.LastName = userObj.LastName;
                        objUser.EmailAddress = userObj.EmailAddress;
                        objUser.ContactNo = userObj.ContactNo;
                        objUser.ManagerName = userObj.ManagerName;
                        objUser.Designation = userObj.Designation;
                        objUser.Status = userObj.Status;
                        objUser.RegistrationDate = DateTime.Now;
                        objUser.DateLastModified = DateTime.Now;
                        objUser.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                        objUser.LanguageId = db.InstanceInfoes.Find(1).DefaultLanguage;
                        if (userObj.OrganisationID != 0) objUser.OrganisationID = userObj.OrganisationID;

                        // check the Optional data depending on selected organisation.
                        var objOrgSettings = db.UserProfileSettingsOrgs.Where(x => x.OrganisationID == userObj.OrganisationID).Select(x => x).ToList();
                        if (objOrgSettings.Count > 0)
                        {
                            var objorgsetprofile1 = objOrgSettings.Where(x => x.ProfileSettingID == 1 && x.OrganisationID == userObj.OrganisationID).ToList();
                            if (objorgsetprofile1.Count > 0)
                            {
                                if (objOrgSettings.Where(x => x.ProfileSettingID == 1).SingleOrDefault().ProfileType == 2)
                                {
                                    if (userObj.Option1 != null) objUser.Option1 = db.UserProfileSettingsOrgValues.Find(Convert.ToInt64(userObj.Option1)).ProfileValuesTitle;
                                }
                                else
                                    objUser.Option1 = (userObj.Option1 != null) ? userObj.Option1 : null;
                            }
                            else
                                objUser.Option1 = (userObj.Option1 != null) ? userObj.Option1 : null;

                            var objorgsetprofile2 = objOrgSettings.Where(x => x.ProfileSettingID == 2 && x.OrganisationID == userObj.OrganisationID).ToList();
                            if (objorgsetprofile2.Count > 0)
                            {
                                if (objOrgSettings.Where(x => x.ProfileSettingID == 2).SingleOrDefault().ProfileType == 2)
                                {
                                    if (userObj.Option2 != null) objUser.Option2 = db.UserProfileSettingsOrgValues.Find(Convert.ToInt64(userObj.Option2)).ProfileValuesTitle;
                                }
                                else
                                    objUser.Option2 = (userObj.Option2 != null) ? userObj.Option2 : null;
                            }
                            else
                                objUser.Option2 = (userObj.Option2 != null) ? userObj.Option2 : null;
                        }

                        objUser.IsDelete = false;
                        objUser.SchoolId = userObj.SchoolID;
                        
                        db.UserProfiles.Add(objUser); // add record in user profile table in data base
                        db.SaveChanges(); // user creation is completed.
                        if (!string.IsNullOrEmpty(userObj.UserIDs))
                        {
                            string[] studentIds = userObj.UserIDs.Split(',');
                            foreach (string studentId in studentIds)
                            {
                                var parentStudent = new ParentStudent();
                                parentStudent.ParentId = objUser.UserId;
                                parentStudent.StudentId = Convert.ToInt64(studentId);
                                parentStudent.AssignedStatus = true;
                                parentStudent.CreatedById = Convert.ToInt64(Session["UserID"]);
                                parentStudent.CreatedDate = DateTime.Now;
                                parentStudent.ModifiedById = Convert.ToInt64(Session["UserID"]);
                                parentStudent.ModifiedDate = DateTime.Now;
                                db.ParentStudents.Add(parentStudent);
                                db.SaveChanges();
                            }
                        }
                        #endregion

                        #region // Role Assignment
                        // default role. Add the default role to user i.e learner.
                        var res = UserManager.AddToRole(user.Id.ToString(), db.InstanceInfoes.Find(1).RoleName);
                        if (ia.Length > 0) // add other roles to user i.e Administrator or group admin.
                            foreach (var x in userObj.UserRolesList.PostedRoles.UserRolesLocalIds)
                            {
                                res = UserManager.AddToRole(user.Id.ToString(), db.AspNetRoles.Find(x).Name);
                            }
                        db.SaveChanges();
                        #endregion

                        #region// Assign to groups

                        foreach (var y in userObj.UserGroupList.PostedGroups.UserGroupsLocalIds) // check the selected group ids that are assigned to user
                        {
                            UserGroup ObjUs = new UserGroup(); // create object of usergroup in which user and group relation ship is saved.
                            ObjUs.UserId = objUser.UserId;
                            ObjUs.GroupID = Convert.ToInt32(y.ToString().Split('~')[0].ToString());
                            ObjUs.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                            ObjUs.DateLastModified = DateTime.Now;
                            db.UserGroups.Add(ObjUs);
                            db.SaveChanges();
                        }

                        #endregion

                        #region // Sending Mail & Redirecting
                        ResetPasswordandSendMail("REGU", userObj.EmailAddress, userObj.Password); // email is send to user with username and password information.

                        Common.updateAssignedUserOfAllGroups();
                        if (userObj.ActionType == 0)
                            return RedirectToAction("Index", "UserManagement"); // redirect to user listing page
                        if (userObj.ActionType == 2)
                            return RedirectToAction("CreateUser", "UserManagement"); // redirect to create new user page
                        if (userObj.ActionType == 1)
                            return RedirectToAction("AssignCourse", "UserManagement", new { id = userObj.UserId }); // redirect to assign course page.
                        #endregion
                    }
                    else // if any error exist at the time of user creation add all the error's in model
                    {
                        var errorMessage = "";
                        foreach (var x in result.Errors)
                            errorMessage += x.ToString();
                        ModelState.AddModelError("EmailAddress", errorMessage);
                    }
                }
                else
                {
                    //ModelState.AddModelError("EmailAddress", @LMSResourse.Admin.User.msgLicenceUserExceed);
                    ModelState.AddModelError("EmailAddress", @LMSResourse.Admin.User.msgMaxUserExceed);
                }
            }
            var UserRoles = new UserRolesLocalView();
            var UserGroup = new UserGroupsLocalView();

            var instinfo = db.InstanceInfoes.Find(1);

            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"])); // user logs in

            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == 0).Select(x => x.GroupID).ToList();

            var selectedRoleLocal = db.AspNetRoles.Where(rol => rol.Name == instinfo.RoleName).Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = true }).OrderByDescending(rol => rol.RoleName).ToList();
            UserRoles.AvailableRoles = db.AspNetRoles.Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = false }).ToList();
            if (IsGroupAdmin) // if current user is group administrator 
            {
                GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentLoginUser).Select(x => x.GroupID).ToList();
                UserRoles.AvailableRoles = db.AspNetRoles.Where(x => x.Name != "Administrator").Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = false }).ToList();
            }
            UserRoles.SelectedRoles = selectedRoleLocal;
            userObj.UserRolesList = UserRoles;

            /// available group for new user and default selected group
            var selectedGroupLocal = db.Groups.Where(g => g.GroupID == 0).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID.ToString(), GroupName = g.GroupName, IsSelected = true }).OrderBy(g => g.GroupName).ToList();
            if (currentuser != null)
            {
                if (UserManager.IsInRole(currentuser.Id, db.AspNetRoles.Find(db.InstanceInfoes.Find(1).GroupAdminId).Name.ToString()))
                {
                    userObj.IsGroupAdmin = true;
                    UserGroup.AvailableGroups = db.Groups.Where(g => g.IsDeleted == false && g.Status == true && g.OrganisationID == currentuser.OrganisationID).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID, GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
                }
                else
                {
                    UserGroup.AvailableGroups = db.Groups.Where(g => g.IsDeleted == false && g.Status == true).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + (g.OrganisationID == null ? "0" : g.OrganisationID.ToString()), GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
                }
                UserGroup.SelectedGroups = selectedGroupLocal;
                userObj.UserGroupList = UserGroup;
            }
            if (userObj.IsGroupAdmin == true) // organisation list for group admin
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true && org.ExpiryDate >= DateTime.Now && org.OrganisationID == currentuser.OrganisationID && org.IsDeleted == false).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true && org.ExpiryDate >= DateTime.Now && org.IsDeleted == false).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            return View(userObj);
        }
        #endregion

        #region // Edit User
        public ActionResult EditUser(int id = 0)
        {
            UserModel us = new UserModel();
            var UserRoles = new UserRolesLocalView();

            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());

            var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == 0).Select(x => x.GroupID).ToList();

            if (IsGroupAdmin)
            {
                GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentLoginUser).Select(x => x.GroupID).ToList();
                // user should be from the group list and should not be a administrator.
                var GroupAdminUsers = db.UserGroups.Where(x => GroupAdminGroups.Contains(x.GroupID) && x.UserProfile.AspNetUser.AspNetUserRoles.Select(y => y.AspNetRole.Name).Contains("Administrator") == false).Select(x => x.UserId).Distinct().ToArray();
                if (!GroupAdminUsers.Contains(id)) // if admin is editing an invalid user then redirect to index page.
                {
                    return RedirectToAction("Index", "UserManagement");
                }
            }


            var userExist = db.UserProfiles.Find(id);
            var currentuser = db.UserProfiles.Find(Convert.ToInt64(Session["UserID"])); // user logs in
            if (userExist == null)
            {
                return RedirectToAction("Index", "UserManagement");
            }
            else if (userExist.IsDelete == true) { return RedirectToAction("Index", "UserManagement"); }
            else if (userExist.IsDelete == false)
            {

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
                us.Status = userExist.Status;
                us.Option1 = userExist.Option1;
                us.Option2 = userExist.Option2;
                us.UserLanguageId = Convert.ToInt32(userExist.LanguageId);
                us.IsGroupAdmin = IsGroupAdmin;                
            }

            var userroles = UserManager.GetRoles(userExist.Id);
            var selectedRolesLocal = db.AspNetRoles.Where(rol => userroles.Contains(rol.Name)).Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = true }).OrderByDescending(rol => rol.RoleName).ToList();
            if (IsGroupAdmin)
                UserRoles.AvailableRoles = db.AspNetRoles.Where(x => x.Name != "Administrator").Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = false }).ToList();
            else
                UserRoles.AvailableRoles = db.AspNetRoles.Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = false }).ToList();
            UserRoles.SelectedRoles = selectedRolesLocal;
            us.ActionType = 0;
            us.UserRolesList = UserRoles;



            var UserGroup = new UserGroupsLocalView();
            var userGroupTemp = db.UserGroups.Where(x => x.UserId == userExist.UserId && (IsGroupAdmin ? GroupAdminGroups.Contains(x.GroupID) : 0 == 0)).Select(x => x.GroupID).ToList();
            var selectedGroupLocal = db.Groups.Where(g => userGroupTemp.Contains(g.GroupID)).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID.ToString(), GroupName = g.GroupName, IsSelected = true }).OrderBy(g => g.GroupName).ToList();
            if (IsGroupAdmin)
            {
                UserGroup.AvailableGroups = db.Groups.Where(g => g.IsDeleted == false && (g.OrganisationID == userExist.OrganisationID || userGroupTemp.Contains(g.GroupID)) && (IsGroupAdmin ? GroupAdminGroups.Contains(g.GroupID) : 0 == 0)).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID, GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
                UserGroup.SelectedGroups = selectedGroupLocal;
            }
            else
            {
                UserGroup.AvailableGroups = db.Groups.Where(g => g.IsDeleted == false && (g.OrganisationID == userExist.OrganisationID || userGroupTemp.Contains(g.GroupID))).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID, GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
                UserGroup.SelectedGroups = selectedGroupLocal;
            }

            us.UserGroupList = UserGroup;

            if (IsGroupAdmin)
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true && org.OrganisationID == userExist.OrganisationID).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName", db.Organisations.Find(us.OrganisationID));

            ViewBag.DistrictList = new SelectList(db.Districts.OrderBy(d => d.DistrictName).Select(d => d), "DistrictId", "DistrictName");
            ViewBag.BlockList = new SelectList(new List<DistrictBlock>(), "BlockId", "BlockName");
            ViewBag.SchoolList = new SelectList(new List<DistrictSchool>(), "SchoolId", "SchoolName");

            int blockId = db.DistrictSchools.Where(s => s.SchoolId == userExist.SchoolId).SingleOrDefault().BlockId;
            us.DistrictID = db.DistrictBlocks.Where(b => b.BlockId == blockId).SingleOrDefault().DistrictId;
            ViewBag.blockId = blockId;
            ViewBag.schoolId = userExist.SchoolId;
            ViewBag.UserIds = string.Join(",", db.ParentStudents.Where(p => p.ParentId == userExist.UserId && p.AssignedStatus.Equals(true))
                .Select(p => p.StudentId.ToString()));

            return View(us);
        }

        [HttpPost]
        public ActionResult EditUser(UserModel userObj)
        {
            #region // If Edited user is  administrator then Redirect to listing Page
            if (userObj.UserId == 1) // i.e can not edit the administrator.
                return RedirectToAction("Index", "UserManagement");

            var objUser = db.UserProfiles.Find(userObj.UserId);
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == 0).Select(x => x.GroupID).ToList();
            if (IsGroupAdmin)
            {
                GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentLoginUser).Select(x => x.GroupID).ToList();
                var GroupAdminUsers = db.UserGroups.Where(x => GroupAdminGroups.Contains(x.GroupID) && x.UserProfile.AspNetUser.AspNetUserRoles.Select(y => y.AspNetRole.Name).Contains("Administrator") == false).Select(x => x.UserId).Distinct().ToArray();
                if (!GroupAdminUsers.Contains(objUser.UserId)) // if admin is editing an invalid user then redirect to index page.
                {
                    return RedirectToAction("Index", "UserManagement");
                }
            }
            #endregion

            if (objUser == null)
            {
                return HttpNotFound();
            }
            else
            {
                Boolean willAdd = true;

                #region // Checking Max user allowed for the current user
                List<int> groupIdLi = new List<int>();
                if (userObj.UserGroupList != null)
                    foreach (var grp in userObj.UserGroupList.PostedGroups.UserGroupsLocalIds)
                    {
                        var tempGroupId = Convert.ToInt32(grp.ToString().Split('~')[0].ToString());
                        if (tempGroupId > 0)
                        {
                            groupIdLi.Add(tempGroupId);
                            var groupDetail = db.Groups.Where(a => a.GroupID == tempGroupId).FirstOrDefault();
                            if (groupDetail.MaxUsers > groupDetail.UserGroups.Where(a => a.UserId != userObj.UserId).Count())
                            { }
                            else
                            { willAdd = false; }
                        }
                    }
                #endregion


                if (willAdd)
                {
                    if (ModelState.IsValid)
                    {
                        string[] ia = { };
                        if (userObj.UserRolesList != null)
                            if (userObj.UserRolesList.PostedRoles != null)
                                if (userObj.UserRolesList.PostedRoles.UserRolesLocalIds.Length > 0)
                                    ia = userObj.UserRolesList.PostedRoles.UserRolesLocalIds;


                        #region // edit users profile fields

                        objUser.EmployeeID = userObj.EmployeeID;
                        objUser.FirstName = userObj.FirstName;
                        objUser.LastName = userObj.LastName;
                        objUser.ContactNo = userObj.ContactNo;
                        objUser.ManagerName = userObj.ManagerName;
                        objUser.Designation = userObj.Designation;
                        objUser.Status = userObj.Status;
                        // objUser.RegistrationDate = DateTime.Now; // As this date will be set only at the time of user creation.
                        objUser.DateLastModified = DateTime.Now;
                        if (userObj.OrganisationID != 0) objUser.OrganisationID = userObj.OrganisationID;

                        var objOrgSettings = db.UserProfileSettingsOrgs.Where(x => x.OrganisationID == userObj.OrganisationID).Select(x => x).ToList();
                        if (objOrgSettings.Count() > 0)
                        {
                            var objorgsetprofile1 = objOrgSettings.Where(x => x.ProfileSettingID == 1 && x.OrganisationID == userObj.OrganisationID).ToList();
                            if (objorgsetprofile1.Count > 0)
                            {
                                if (objOrgSettings.Where(x => x.ProfileSettingID == 1).SingleOrDefault().ProfileType == 2)
                                {
                                    if (userObj.Option1 != null) objUser.Option1 = db.UserProfileSettingsOrgValues.Find(Convert.ToInt64(userObj.Option1)).ProfileValuesTitle;
                                }
                                else
                                    objUser.Option1 = (userObj.Option1 != null) ? userObj.Option1 : null;
                            }
                            else
                                objUser.Option1 = (userObj.Option1 != null) ? userObj.Option1 : null;

                            var objorgsetprofile2 = objOrgSettings.Where(x => x.ProfileSettingID == 2 && x.OrganisationID == userObj.OrganisationID).ToList();
                            if (objorgsetprofile2.Count > 0)
                            {
                                if (objOrgSettings.Where(x => x.ProfileSettingID == 2).SingleOrDefault().ProfileType == 2)
                                {
                                    if (userObj.Option2 != null) objUser.Option2 = db.UserProfileSettingsOrgValues.Find(Convert.ToInt64(userObj.Option2)).ProfileValuesTitle;
                                }
                                else
                                    objUser.Option2 = (userObj.Option2 != null) ? userObj.Option2 : null;
                            }
                            else
                                objUser.Option2 = (userObj.Option2 != null) ? userObj.Option2 : null;
                        }

                        objUser.SchoolId = userObj.SchoolID;
                        db.SaveChanges();

                        if (!string.IsNullOrEmpty(userObj.UserIDs))
                        {
                            string[] studentIds = userObj.UserIDs.Split(',');
                            Int64 studId = 0;

                            (from ps in db.ParentStudents
                             where ps.ParentId == objUser.UserId
                             select ps).ToList().ForEach(a => a.AssignedStatus = false);
                            db.SaveChanges();

                            foreach (string studentId in studentIds)
                            {
                                studId = Convert.ToInt64(studentId);
                                var parentStudent = db.ParentStudents.Where(a => a.ParentId.Equals(objUser.UserId) && a.StudentId.Equals(studId)).FirstOrDefault();
                                if (parentStudent == null)
                                {
                                    parentStudent = new ParentStudent();
                                    parentStudent.ParentId = objUser.UserId;
                                    parentStudent.StudentId = Convert.ToInt64(studentId);
                                    parentStudent.AssignedStatus = true;
                                    parentStudent.CreatedById = Convert.ToInt64(Session["UserID"]);
                                    parentStudent.CreatedDate = DateTime.Now;
                                    parentStudent.ModifiedById = Convert.ToInt64(Session["UserID"]);
                                    parentStudent.ModifiedDate = DateTime.Now;
                                    db.ParentStudents.Add(parentStudent);
                                    db.SaveChanges();
                                }
                                else
                                {
                                    parentStudent = db.ParentStudents.Where(a => a.ParentId.Equals(objUser.UserId) && a.StudentId.Equals(studId)).FirstOrDefault();
                                    parentStudent.AssignedStatus = true;
                                    db.SaveChanges();
                                }                                
                            }
                        }
                        #endregion

                        #region // Role assignment
                        //// default role                    
                        //if (ia.Length > 0)
                        //    foreach (var x in userObj.UserRolesList.PostedRoles.UserRolesLocalIds)
                        //    {

                        if (IsGroupAdmin && objUser.UserId != currentLoginUser) // group adminstrator can change it's role.
                            SetRolesToUser(objUser.EmailAddress, (userObj.UserRolesList == null ? null : userObj.UserRolesList.PostedRoles));
                        else if (IsGroupAdmin == false && objUser.UserId != currentLoginUser) // Administrator can change the roles of other, but himself.
                            SetRolesToUser(objUser.EmailAddress, (userObj.UserRolesList == null ? null : userObj.UserRolesList.PostedRoles));
                        //    }
                        ////
                        #endregion

                        #region // group change

                        int[] postedgroups = { };
                        if (userObj.UserGroupList.PostedGroups != null)
                            if (userObj.UserGroupList.PostedGroups.UserGroupsLocalIds.Length > 0)
                                postedgroups = userObj.UserGroupList.PostedGroups.UserGroupsLocalIds.Select(x => int.Parse(x.Split('~')[0].ToString())).ToArray();

                        // Get the groups already assigned
                        // if current user is group administrator then only the assigned and group admin groups will come in assigned grpups.
                        int[] assignedGroups = db.UserGroups.Where(x => x.UserId == objUser.UserId && (IsGroupAdmin ? GroupAdminGroups.Contains(x.GroupID) : 0 == 0)).Select(x => x.GroupID).ToArray();
                        // Get the groups which have to assigned according to selection on webpage
                        var GroupsToAssigne = postedgroups.Except(assignedGroups);

                        foreach (var y in GroupsToAssigne)
                        {
                            UserGroup ObjUs;
                            ObjUs = db.UserGroups.Where(x => x.GroupID == y && x.UserId == objUser.UserId).FirstOrDefault();
                            if (ObjUs == null)
                            {
                                ObjUs = new UserGroup();
                                ObjUs.UserId = objUser.UserId;
                                ObjUs.GroupID = y;
                                ObjUs.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                                ObjUs.DateLastModified = DateTime.Now;
                                db.UserGroups.Add(ObjUs);
                                db.SaveChanges();
                            }
                        }

                        // remove assigned groups which are not checked or unchecked.
                        // refresh the assigned groups
                        assignedGroups = db.UserGroups.Where(x => x.UserId == objUser.UserId && (IsGroupAdmin ? GroupAdminGroups.Contains(x.GroupID) : 0 == 0)).Select(x => x.GroupID).ToArray();
                        // Get the groups which have to unassign according to selection on webpage
                        var GroupsToRemove = assignedGroups.Except(postedgroups);

                        var ObjGroupsToRemove = (from x in db.UserGroups
                                                 where x.UserId == objUser.UserId && GroupsToRemove.Contains(x.GroupID)
                                                 orderby x.UserId, x.GroupID
                                                 select x).ToList();
                        foreach (var x in ObjGroupsToRemove)
                        {
                            var objremove = db.UserGroups.Find(x.UserGroupId);
                            db.UserGroups.Remove(objremove);
                            db.SaveChanges();
                        }


                        #endregion

                        #region // If admin change the password then send mail to user

                        if (string.IsNullOrEmpty(userObj.Password) == false)
                        {
                            ResetPasswordandSendMail("GNPASS", userObj.EmailAddress, userObj.Password);
                        }
                        #endregion

                        Common.updateAssignedUserOfAllGroups();

                        #region // Redirecting after update
                        if (userObj.ActionType == 0)
                            return RedirectToAction("Index", "UserManagement");
                        if (userObj.ActionType == 2)
                            return RedirectToAction("CreateUser", "UserManagement");
                        if (userObj.ActionType == 1)
                            return RedirectToAction("Index", "UserManagement");
                        #endregion

                    }
                }
                else
                {
                    ModelState.AddModelError("EmailAddress", LMSResourse.Admin.User.msgMaxUserExceed);
                }


                #region //  Initializing User groups & Roles
                var userroles = UserManager.GetRoles(objUser.Id);
                userObj.UserRolesList = userObj.UserRolesList == null ? new UserRolesLocalView() : userObj.UserRolesList;
                var selectedRolesLocal = db.AspNetRoles.Where(rol => userroles.Contains(rol.Name)).Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = true }).OrderByDescending(rol => rol.RoleName).ToList();
                userObj.UserRolesList.AvailableRoles = db.AspNetRoles.Select(rol => new UserRolesLocal { RoleId = rol.Id, RoleName = rol.Name, IsSelected = false }).ToList();
                userObj.UserRolesList.SelectedRoles = selectedRolesLocal;
                userObj.ActionType = 0;

                var UserGroup = new UserGroupsLocalView();
                var userGroupTemp = db.UserGroups.Where(x => x.UserId == userObj.UserId && (IsGroupAdmin ? GroupAdminGroups.Contains(x.GroupID) : 0 == 0)).Select(x => x.GroupID).ToList();
                foreach (var postedGroupID in groupIdLi)
                {
                    if (userGroupTemp.Contains(postedGroupID))
                    {

                    }
                    else
                    {
                        userGroupTemp.Add(postedGroupID);
                    }
                }
                var selectedGroupLocal = db.Groups.Where(g => userGroupTemp.Contains(g.GroupID)).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID.ToString(), GroupName = g.GroupName, IsSelected = true }).OrderBy(g => g.GroupName).ToList();
                if (IsGroupAdmin)
                {
                    UserGroup.AvailableGroups = db.Groups.Where(g => g.IsDeleted == false && (g.OrganisationID == userObj.OrganisationID || userGroupTemp.Contains(g.GroupID)) && (IsGroupAdmin ? GroupAdminGroups.Contains(g.GroupID) : 0 == 0)).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID, GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
                    UserGroup.SelectedGroups = selectedGroupLocal;
                }
                else
                {
                    UserGroup.AvailableGroups = db.Groups.Where(g => g.IsDeleted == false && (g.OrganisationID == userObj.OrganisationID || userGroupTemp.Contains(g.GroupID))).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString() + "~" + g.OrganisationID, GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
                    UserGroup.SelectedGroups = selectedGroupLocal;
                }

                userObj.UserGroupList = UserGroup;

                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName", db.Organisations.Find(objUser.OrganisationID));
                #endregion

            }
            return View(userObj);

        }

        #endregion

        #region // Delete user
        [HttpPost]
        public string DeleteUser(int id = 0)
        {
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var willDelete = false;
            if (currentLoginUser != id) // user can't delete itself.
            {
                bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == 0).Select(x => x.GroupID).ToList();
                if (IsGroupAdmin)
                {
                    GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentLoginUser).Select(x => x.GroupID).ToList();
                    var GroupAdminUsers = db.UserGroups.Where(x => GroupAdminGroups.Contains(x.GroupID)).Select(x => x.UserId).Distinct().ToArray();
                    if (!GroupAdminUsers.Contains(id)) // if admin is deleting an invalid user then redirect to index page.
                    {
                        return LMSResourse.Admin.User.msgDelete_User_NotValidRights;
                    }
                    else
                    {
                        var UserTobeDeleted = db.UserProfiles.Find(id);
                        var aspNetUser = db.AspNetUsers.Find(UserTobeDeleted.Id);
                        var cUser = db.UserProfiles.Find(currentLoginUser);
                        if (UserTobeDeleted != null && aspNetUser != null)
                        {
                            UserTobeDeleted.Id = null;
                            UserTobeDeleted.IsDelete = true;
                            UserTobeDeleted.DeleteInformation = aspNetUser.Id.ToString() + " : " + aspNetUser.UserName + " is delete by userName : " + cUser.EmailAddress + " on date" + DateTime.Now.ToString();
                            db.SaveChanges();
                            db.AspNetUsers.Remove(aspNetUser);
                            db.SaveChanges();
                            willDelete = true;
                        }
                    }
                }
                else
                {
                    var UserTobeDeleted = db.UserProfiles.Find(id);
                    var aspNetUser = db.AspNetUsers.Find(UserTobeDeleted.Id);
                    var cUser = db.UserProfiles.Find(currentLoginUser);
                    if (UserTobeDeleted != null && aspNetUser != null)
                    {
                        UserTobeDeleted.Id = null;
                        UserTobeDeleted.IsDelete = true;
                        UserTobeDeleted.DeleteInformation = aspNetUser.Id.ToString() + " : " + aspNetUser.UserName + " is delete by userName : " + cUser.EmailAddress + " on date" + DateTime.Now.ToString();
                        db.SaveChanges();
                        db.AspNetUsers.Remove(aspNetUser);
                        db.SaveChanges();
                        willDelete = true;
                    }
                }
                if (willDelete)
                {
                    #region //Archive user course data to ElearningStatusArchive table
                    //Archive user course data to ElearningStatusArchive table
                    var sqlqueryScore = "Select * from s_score" +
                                           " Where idCommunication IN(Select idCommunication From s_communication Where studentid =" + id + ")";

                    IEnumerable<s_score> ObjScoreList = null;
                    ObjScoreList = db.Database.SqlQuery<s_score>(sqlqueryScore).ToList();
                    if (ObjScoreList.Count() > 0)
                    {
                        foreach (var score in ObjScoreList)
                        {
                            var objScore = db.s_score.Find(score.idScore);
                            db.s_score.Remove(objScore);
                            db.SaveChanges();
                        }
                    }

                    var IsUserExistInSComm = db.s_communication.Where(a => a.studentid == id).ToList();
                    if (IsUserExistInSComm != null && IsUserExistInSComm.Count() > 0)
                    {
                        db.s_communication.RemoveRange(IsUserExistInSComm);
                        db.SaveChanges();
                    }

                    var sqlquery = @"SELECT	ElearningStatus.CourseID,ElearningStatus.UserID,TimeInCourse,TotalHits,LoginDate,LogoutDate,ElearningStatus.[Status],PassPercent,MaxScore,MinScore,Score,GroupCourse.ExpiryDate,GETDATE()
                               FROM ElearningStatus JOIN UserGroup
                                ON UserGroup.UserId = ElearningStatus.UserID JOIN[Group]
                                ON [Group].GroupID = UserGroup.GroupID AND [Group].IsDeleted = 0 JOIN GroupCourse
                                ON GroupCourse.GroupID = [Group].GroupID AND GroupCourse.CourseId = ElearningStatus.CourseId AND GroupCourse.AssignedStatus = 1 JOIN Course
                                ON Course.CourseId = GroupCourse.CourseId AND Course.IsDeleted = 0 AND Course.IsFinalized = 1 JOIN UserProfile
                                ON UserProfile.UserId = UserGroup.UserId
                                WHERE ElearningStatus.IsQuiz IS NULL AND ElearningStatus.UserID = @P0
                                UNION
                                SELECT  ElearningStatus.CourseID,ElearningStatus.UserID,TimeInCourse,TotalHits,LoginDate,LogoutDate,ElearningStatus.[Status],ElearningStatus.PassPercent,MaxScore,MinScore,Score,GroupQuiz.ExpiryDate,GETDATE()
                                FROM ElearningStatus JOIN UserGroup
                                ON UserGroup.UserId = ElearningStatus.UserID JOIN[Group]
                                ON [Group].GroupID = UserGroup.GroupID AND [Group].IsDeleted = 0 JOIN GroupQuiz
                                ON GroupQuiz.GroupID = [Group].GroupID AND GroupQuiz.QuizId = ElearningStatus.CourseId AND GroupQuiz.AssignedStatus = 1 JOIN Quizes
                                ON Quizes.QuizId = GroupQuiz.QuizId JOIN UserProfile
                                ON UserProfile.UserId = UserGroup.UserId
                                WHERE ElearningStatus.IsQuiz = 1 AND ElearningStatus.UserID = @P0";

                    IEnumerable<clsElearningStatus> ObjElearning = null;
                    ObjElearning = db.Database.SqlQuery<clsElearningStatus>(sqlquery,id).ToList();
                    if (ObjElearning.Count() > 0)
                    {
                        foreach (var elr in ObjElearning)
                        {
                            ElearningStatusArchive elrArc = new ElearningStatusArchive();
                            elrArc.CourseId = elr.CourseId;
                            elrArc.UserID = elr.UserId;
                            elrArc.TimeInCourse = elr.TimeInCourse;
                            elrArc.TotalHits = elr.TotalHits;
                            elrArc.LoginDate = elr.LoginDate;
                            elrArc.LogoutDate = elr.LogoutDate;
                            elrArc.Status = elr.Status;
                            elrArc.PassPercent = elr.PassPercent;
                            elrArc.MaxScore = elr.MaxScore;
                            elrArc.MinScore = elr.MinScore;
                            elrArc.Score = elr.Score;
                            elrArc.ExpiryDate = elr.ExpiryDate;
                            elrArc.CreationDate = DateTime.Now;
                            db.ElearningStatusArchives.Add(elrArc);
                            db.SaveChanges(); // save the ElearningStatus data in ElearningStatusArchives table
                        }
                    }

                    var sqlquery1 = @"SELECT	ElearningStatus.CourseID,ElearningStatus.UserID,TimeInCourse,TotalHits,LoginDate,LogoutDate,ElearningStatus.[Status],PassPercent,MaxScore,MinScore,Score,UserCourse.ExpiryDate,GETDATE() 
                                FROM	ElearningStatus JOIN UserCourse 
                                ON		UserCourse.UserId = ElearningStatus.UserID AND UserCourse.CourseId = ElearningStatus.CourseId AND UserCourse.AssignedStatus = 1 JOIN [Course] 
                                ON		[Course].CourseId = UserCourse.CourseId AND [Course].IsDeleted = 0  AND Course.IsFinalized = 1 JOIN UserProfile 
                                ON		UserProfile.UserId = UserCourse.UserId 
                                WHERE	ElearningStatus.IsQuiz IS NULL AND ElearningStatus.UserID = @P0
                                UNION
                                SELECT	ElearningStatus.CourseID,ElearningStatus.UserID,TimeInCourse,TotalHits,LoginDate,LogoutDate,ElearningStatus.[Status],ElearningStatus.PassPercent,MaxScore,MinScore,Score,UserQuiz.ExpiryDate,GETDATE() 
                                FROM	ElearningStatus JOIN UserQuiz 
                                ON		UserQuiz.UserId = ElearningStatus.UserID AND UserQuiz.QuizId = ElearningStatus.CourseId AND UserQuiz.AssignedStatus = 1 JOIN [Quizes] 
                                ON		[Quizes].QuizId = UserQuiz.QuizId JOIN UserProfile 
                                ON		UserProfile.UserId = UserQuiz.UserId 
                                WHERE	ElearningStatus.IsQuiz = 1 AND ElearningStatus.UserID = @P0";

                    IEnumerable<clsElearningStatus> ObjElearning1 = null;
                    ObjElearning1 = db.Database.SqlQuery<clsElearningStatus>(sqlquery1, id).ToList();
                    if (ObjElearning1.Count() > 0)
                    {
                        foreach (var elr in ObjElearning1)
                        {
                            ElearningStatusArchive elrArc = new ElearningStatusArchive();
                            elrArc.CourseId = elr.CourseId;
                            elrArc.UserID = elr.UserId;
                            elrArc.TimeInCourse = elr.TimeInCourse;
                            elrArc.TotalHits = elr.TotalHits;
                            elrArc.LoginDate = elr.LoginDate;
                            elrArc.LogoutDate = elr.LogoutDate;
                            elrArc.Status = elr.Status;
                            elrArc.PassPercent = elr.PassPercent;
                            elrArc.MaxScore = elr.MaxScore;
                            elrArc.MinScore = elr.MinScore;
                            elrArc.Score = elr.Score;
                            elrArc.ExpiryDate = elr.ExpiryDate;
                            elrArc.CreationDate = DateTime.Now;
                            db.ElearningStatusArchives.Add(elrArc);
                            db.SaveChanges(); // save the ElearningStatus data in ElearningStatusArchives table
                        }
                    }

                    var IsUserExistInElearning = db.ElearningStatus.Where(a => a.UserID == id).ToList();
                    if (IsUserExistInElearning != null && IsUserExistInElearning.Count() > 0)
                    {
                        db.ElearningStatus.RemoveRange(IsUserExistInElearning);
                        db.SaveChanges();
                    }
                    #endregion

                    var IsUserExistInGroups = db.UserGroups.Where(a => a.UserId == id).ToList();
                    if (IsUserExistInGroups != null && IsUserExistInGroups.Count() > 0)
                    {
                        //Comment below lines due to don't remove user groups association otherwise user course archive report will not display this user's data.

                        //db.UserGroups.RemoveRange(IsUserExistInGroups);
                        //db.SaveChanges();
                        Common.updateAssignedUserOfAllGroups();
                    }
                }
            }
            return "";
        }
        #endregion

        #region // Assign courses to user
        [HttpGet]
        public ActionResult AjaxHandlerAssignedCourses(jQueryDataTableParamModel param)
        {
            var sortColumnIndex = Convert.ToInt32(Request["iSortCol_0"]);
            Func<AssignedCourse, string> orderingFunction = (c => sortColumnIndex == 1 ? c.CourseName.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 2 ? c.CategoryName.TrimEnd().TrimStart().ToLower() :
                                                            sortColumnIndex == 3 ? c.CertificateName.TrimEnd().TrimStart().ToLower() :
                                                            c.CourseName.ToLower());
            var sortDirection = Request["sSortDir_0"];
            IEnumerable<AssignedCourse> filterUserAssignedCourse = null;
            var UserId = Int64.Parse(param.iD.ToString());
            int languageId = 0;
            languageId = int.Parse(Session["LanguageId"].ToString());
            try
            {
                var LoginUserID = Convert.ToInt64(Session["UserID"].ToString());
                var _PData = db.GetAssignedUserCourse(UserId, languageId, LoginUserID);
                var tempx = from x in _PData
                            select new AssignedCourse
                            {
                                CourseId = x.CourseId,
                                CourseName = x.CourseName,
                                CategoryName = x.CategoryName,
                                CertificateName = x.CertificateName,
                                AssignedStatus = (x.AssignedStatus == null) ? false : (bool)x.AssignedStatus,
                                ExpiryDate = x.ExpiryDate,
                                IsGroupAssigned = (x.IsGroupAssigned == true) ? 1 : 0,
                                TotalHits = Convert.ToInt32(x.TotalHits)
                            };
                filterUserAssignedCourse = tempx.ToList<AssignedCourse>();

                /// search action
                if (!string.IsNullOrEmpty(param.sSearch))
                {
                    filterUserAssignedCourse = from x in filterUserAssignedCourse
                                               where x.CourseName.ToLower().Contains(param.sSearch.ToLower()) || x.CertificateName.ToLower().Contains(param.sSearch.ToLower()) || x.CategoryName.ToLower().Contains(param.sSearch.ToLower())
                                               select x;

                }
                else
                {
                    filterUserAssignedCourse = from x in filterUserAssignedCourse
                                               select x;
                }

                // ordering action

                if (sortDirection == "asc")
                {
                    filterUserAssignedCourse = filterUserAssignedCourse.OrderBy(orderingFunction);
                }
                else if (sortDirection == "desc")
                {
                    filterUserAssignedCourse = filterUserAssignedCourse.OrderByDescending(orderingFunction);
                }

                filterUserAssignedCourse = filterUserAssignedCourse.ToList();

                // records to display            
                var displayedUserAssignedCourse = filterUserAssignedCourse.Skip(param.iDisplayStart).Take(param.iDisplayLength);
                if (param.iDisplayLength == -1)
                    displayedUserAssignedCourse = filterUserAssignedCourse;

                var result = from obj in displayedUserAssignedCourse.ToList()
                             select new[] {
                              obj.CourseName,
                              obj.CategoryName,
                              obj.CertificateName,
                              obj.ExpiryDate==null?"":((DateTime)obj.ExpiryDate).ToString(ConfigurationManager.AppSettings["dateformatForCalanderServerSide"].ToString(),CultureInfo.InvariantCulture),
                              obj.AssignedStatus.ToString(),
                              Convert.ToString(obj.CourseId),
                              Convert.ToString(obj.IsGroupAssigned),
                              Convert.ToString(obj.TotalHits),
                              obj.AssignedStatus.ToString()
                          };

                return Json(new
                {
                    sEcho = param.sEcho,
                    iTotalRecords = filterUserAssignedCourse.Count(),
                    iTotalDisplayRecords = filterUserAssignedCourse.Count(),
                    aaData = result
                },
                               JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {

            }
            return Json(new
            {

            },
                              JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult AssignCourse(int id)
        {
            var Userexist = db.UserProfiles.Find(id);
            UserAssignedCourse us = new UserAssignedCourse();

            if (Userexist != null)
            {
                us.UserId = Userexist.UserId;
                us.UserName = Userexist.FirstName + " " + Userexist.LastName;
                us.DateFormatForClientSide = ConfigurationManager.AppSettings["dateformatForCalanderClientSide"].ToString();

            }
            return View(us);
        }
        
        #endregion

        #region // other function
        public void SetRolesToUser(string UserName, PostedUserRolesLocal assignedRoles)
        {
            // check user in profile table
            var objUser = db.UserProfiles.Where(us => us.EmailAddress == UserName && us.Id != null).First();
            // check user in aspnetusers table
            var UserExists = UserManager.FindByName(UserName);
            var defaultRoleName = db.InstanceInfoes.Find(1).RoleName;
            var defaultRole = db.AspNetRoles.Where(x => x.Name == defaultRoleName).FirstOrDefault();

            if (objUser != null && UserExists != null)
            {
                var allRoles = db.AspNetRoles.Select(x => x).ToList();
                var RolesAssignedToUser = UserManager.GetRoles(objUser.Id).ToList();
                if (assignedRoles == null) assignedRoles = new PostedUserRolesLocal();
                if (assignedRoles.UserRolesLocalIds == null) assignedRoles.UserRolesLocalIds = new string[] { defaultRole.Name };
                if (assignedRoles != null)
                {
                    foreach (var usrole in allRoles)
                    {
                        //if (usrole.Name != defaultRole.Name)
                        //{
                            if (assignedRoles.UserRolesLocalIds.Contains(usrole.Id))
                            {
                                // first check role for user (if not then) assign roles 
                                if (RolesAssignedToUser.Contains(usrole.Name) == false)
                                {
                                    UserManager.AddToRole(objUser.Id.ToString(), usrole.Name);
                                }

                            }
                            else
                            {


                                // unassign roles
                                if (RolesAssignedToUser.Contains(usrole.Name) == true)
                                {
                                    UserManager.RemoveFromRole(objUser.Id.ToString(), usrole.Name);
                                }

                            }
                            db.SaveChanges();
                        //}
                    }
                }
                // if defaul role is not assigned then assigne the default role.
                if (RolesAssignedToUser.Contains(defaultRole.Name) == false)
                { UserManager.AddToRole(objUser.Id.ToString(), defaultRole.Name); }
                db.SaveChanges();

            }
        }

        public void ResetPasswordandSendMail(string MailCode, string UserName, string Password = "")
        {
            #region // If admin change the password or Request for forgot password then send mail to user and set the IsRegisterMailSend to true

            var objUser = db.UserProfiles.Where(us => us.EmailAddress == UserName && us.Id != null && us.IsDelete == false).FirstOrDefault();
            var UserExists = UserManager.FindByName(UserName);

            if (objUser != null && UserExists != null)
            {
                UserManager.RemovePassword(objUser.Id);
                if (string.IsNullOrWhiteSpace(Password))
                    Password = AjaxGeneratePassword();
                UserManager.AddPassword(objUser.Id, Password);
                var objInstance = (from o in db.InstanceInfoes
                                   where o.InstanceID == 1
                                   select new { o.InstanceTitle, o.HostEmail, o.URL, o.SmtpIPv4, o.SupportEmail }).FirstOrDefault();
                if (objInstance != null)
                {
                    var objEmail = (from ol in db.Emails
                                    where ol.MailCode == MailCode && ol.IsOn == true && ol.OrganisationID == objUser.OrganisationID
                                    select new { ol.Subject, ol.Body, ol.ID, ol.FromEmail }).FirstOrDefault();

                    //if (objEmail != null && objUser.Status == true && objUser.IsRegisterMailSend == false)
                    if (objEmail != null && objUser.Status == true)
                    {
                        string fromEmail = objEmail.FromEmail;

                        string subject = objEmail.Subject.Replace("{InstanceTitle}", objInstance.InstanceTitle);

                        //var resetLink = "<a href=" + objInstance.URL +"Account/manage>" + "click here</a>";

                        string body = objEmail.Body;

                        body = body.Replace("{InstanceTitle}", objInstance.InstanceTitle).Replace("{InstanceURL}", objInstance.URL).Replace("{FirstName}", HttpUtility.HtmlEncode(objUser.FirstName)).Replace("{UserName}", HttpUtility.HtmlEncode(objUser.EmailAddress)).Replace("{Password}", Password).Replace("{Password}", Password).Replace("{SupportMail}", objInstance.SupportEmail); //edit it
                        try
                        {
                            MailEngine.Send(fromEmail, objUser.EmailAddress, subject, body, objInstance.SmtpIPv4);
                            MailEngine oLog = new MailEngine();
                            oLog.LogEmail(fromEmail, objEmail.ID, objUser.UserId);
                            objUser.IsRegisterMailSend = true;
                            db.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                        }

                    }
                }

            }
            #endregion
        }

        public string SendMailToUser(string MailCode, string UserName, string URL = "")
        {
            string returnVal = "";
            #region // If admin change the password or Request for forgot password then send mail to user and set the IsRegisterMailSend to true

            var objUser = db.UserProfiles.Where(us => us.EmailAddress == UserName && us.Id != null).FirstOrDefault(); //&& us.IsDelete == false
            var UserExists = db.AspNetUsers.Where(x => x.UserName == UserName);

            if (objUser != null && UserExists != null)
            {
                var objInstance = (from o in db.InstanceInfoes
                                   where o.InstanceID == 1
                                   select new { o.InstanceTitle, o.HostEmail, o.URL, o.SmtpIPv4, o.SupportEmail }).FirstOrDefault();


                var objEmail = (from ol in db.Emails
                                where ol.MailCode == MailCode && ol.IsOn == true
                                select new { ol.Subject, ol.Body, ol.ID, ol.FromEmail }).FirstOrDefault();

                //if (objEmail != null && objUser.Status == true && objUser.IsRegisterMailSend == false)
                if (objEmail != null && objUser.Status == true)
                {
                    string fromEmail = objEmail.FromEmail;
                    string apptitle = objInstance.InstanceTitle;
                    string appurl = objInstance.URL;
                    string smtpip = objInstance.SmtpIPv4;

                    string subject = objEmail.Subject.Replace("{InstanceTitle}", apptitle);

                    string mailcontent = objEmail.Body;

                    mailcontent = mailcontent.Replace("{InstanceTitle}", apptitle).Replace("{InstanceURL}", objInstance.URL).Replace("{FirstName}", HttpUtility.HtmlEncode(objUser.FirstName)).Replace("{UserName}", HttpUtility.HtmlEncode(objUser.EmailAddress)).Replace("{ResetPasswordURL}", URL).Replace("{SupportMail}", objInstance.SupportEmail); //edit it
                    try
                    {


                        MailEngine.Send(fromEmail, objUser.EmailAddress, subject, mailcontent, objInstance.SmtpIPv4);
                        MailEngine oLog = new MailEngine();
                        oLog.LogEmail(fromEmail, objEmail.ID, objUser.UserId);
                        objUser.IsRegisterMailSend = false;
                        db.SaveChanges();
                        returnVal = ErrorCodes.ProcessCompleted.ToString();
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                }
                else
                {
                    returnVal = ErrorCodes.BadRequest.ToString();
                }
            }
            else
            {
                returnVal = ErrorCodes.NoRecordExist.ToString();
            }
            #endregion

            return returnVal;
        }


        public string AjaxGeneratePassword()
        {
            return System.Web.Security.Membership.GeneratePassword(6, 2);
        }
        #endregion

        #region // Import User
        public void CreateUserTempleate(int id)
        {
            using (StringWriter sw = new StringWriter())
            {
                using (HtmlTextWriter htw = new System.Web.UI.HtmlTextWriter(sw))
                {
                    HtmlTable ProfileTable = new HtmlTable();

                    HtmlTableRow trProfileRow = new HtmlTableRow();
                    HtmlTableCell tdProfileCell = new HtmlTableCell();
                    Label lblUniqueIdentifier = new Label();

                    ProfileTable.Style.Add("border", "1");
                    ProfileTable.Style.Add("border-width", "1");
                    ProfileTable.Style.Add("border-color", "Black");

                    lblUniqueIdentifier.Text = " *";

                    var defaultLanguage = db.InstanceInfoes.Find(1).DefaultLanguage;
                    var Org = db.Organisations.Find(id);

                    var dtUserProfile = from UProfile in db.UserProfileSettings
                                        join usSetOrg in db.UserProfileSettingsOrgs on UProfile.ProfileSettingID equals usSetOrg.ProfileSettingID
                                        join usSetOrgInfo in db.UserProfileSettingsInfoes on usSetOrg.ProfileSettignOrgId equals usSetOrgInfo.ProfileSettignOrgId
                                        where usSetOrgInfo.LanguageID == defaultLanguage && usSetOrg.OrganisationID == Org.OrganisationID
                                        orderby usSetOrg.ProfileSettingID
                                        select new { usSetOrg.ProfileSettingID, usSetOrgInfo.ProfileTitle, usSetOrg.IsDisplay, usSetOrg.IsMandatory };

                    // UserName
                    //tdProfileCell = new HtmlTableCell();
                    //tdProfileCell.InnerText = "UserName";
                    //tdProfileCell.Width = "150";
                    //tdProfileCell.Style.Add("font-family", "Arial");
                    //tdProfileCell.Style.Add("font-size", "12px");
                    //tdProfileCell.Style.Add("font-weight", "bold");
                    //tdProfileCell.Style.Add("background", "#999999");
                    //lblUniqueIdentifier = new Label();

                    //lblUniqueIdentifier.Text = " *";
                    //tdProfileCell.Controls.Add(lblUniqueIdentifier);
                    //trProfileRow.Cells.Add(tdProfileCell);


                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "FirstName";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);


                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "LastName";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();
                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);
                    trProfileRow.Cells.Add(tdProfileCell);


                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "Email";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "IsActive";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();
                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "Role";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "Organisation";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    //lblUniqueIdentifier.Text = " *";
                    //tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "Group";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    foreach (var us in dtUserProfile.ToList())
                    {
                        if (us.IsDisplay == true)
                        {

                            tdProfileCell = new HtmlTableCell();
                            tdProfileCell.InnerText = us.ProfileTitle.ToString().TrimEnd().TrimStart();
                            tdProfileCell.Width = "150";
                            tdProfileCell.Style.Add("font-family", "Arial");
                            tdProfileCell.Style.Add("font-size", "12px");
                            tdProfileCell.Style.Add("font-weight", "bold");
                            tdProfileCell.Style.Add("background", "#999999");
                            if (us.IsMandatory == true)
                            {
                                lblUniqueIdentifier = new Label();

                                lblUniqueIdentifier.Text = " *";
                                tdProfileCell.Controls.Add(lblUniqueIdentifier);
                            }


                            trProfileRow.Cells.Add(tdProfileCell);

                        }
                    }
                    ProfileTable.Rows.Add(trProfileRow);

                    Response.ContentType = "application/ms-excel";
                    Response.AddHeader("content-disposition", "attachment; filename=ImportUsers.xls;");


                    ProfileTable.RenderControl(htw);
                    Response.Write(sw.ToString());
                    Response.End();
                }
            }




        }


        public ActionResult ImportUser()
        {
            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var currentuser = db.UserProfiles.Find(currentLoginUser);
            if (IsGroupAdmin == true)
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true && org.IsDeleted == false && org.OrganisationID == currentuser.OrganisationID).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            else
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true && org.IsDeleted == false).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName");
            }
            ImportUser Imp = new ImportUser();
            Imp.ActionType = 0;
            return View(Imp);
        }

        [HttpPost]
        public ActionResult ImportUser(ImportUser Imp)
        {


            bool IsGroupAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2);
            bool IsSuperAdmin = (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(1);
            var currentLoginUser = Convert.ToInt64(Session["UserID"].ToString());
            var currentuser = db.UserProfiles.Find(currentLoginUser);

            var AllAvailableRoles = db.AspNetRoles.Select(x => x.Name.TrimEnd().TrimStart().ToLower()).ToList();
            var Org = db.Organisations.Find(Imp.OrganisationID);
            var orgId = (Imp.OrganisationID != null && Imp.OrganisationID > 0 ? Org.OrganisationID : 0);

            var AllAvailableGroups = db.Groups.Where(x => x.OrganisationID == orgId).Select(x => x.GroupName.ToLower().TrimEnd().TrimStart()).ToList();
            if (IsGroupAdmin == true)
            {
                AllAvailableGroups = currentuser.UserGroups.Select(x => x.Group.GroupName.ToLower().TrimEnd().TrimStart()).ToList();
                AllAvailableRoles = db.AspNetRoles.Where(x => x.Name != "Administrator").Select(x => x.Name.TrimEnd().TrimStart().ToLower()).ToList();
            }


            if (IsGroupAdmin == true)
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true && org.OrganisationID == currentuser.OrganisationID).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName", Imp.OrganisationID);
            }
            else
            {
                ViewBag.OrgList = new SelectList(db.Organisations.Where(org => org.Status == true).OrderBy(org => org.OrganisationName).Select(org => org), "OrganisationID", "OrganisationName", Imp.OrganisationID);
            }
            if (Imp.ActionType == 1)
            {
                CreateUserTempleate(Imp.OrganisationID);
            }
            else if (Imp.ActionType == 2)
            {
                if (Imp.OrganisationID == 0)
                {
                    CreateUserTempleate(1);
                }
                else
                {

                }
                CreateUserTempleateWithData(Imp.OrganisationID);
            }
            else
            {
                DataTable dtTempUsers = new DataTable();

                if (ModelState.IsValid)
                {
                    try
                    {
                        #region // File Uploading

                        string extension = System.IO.Path.GetExtension(Imp.file.FileName);

                        string fname = "";
                        do
                        {
                            fname = Guid.NewGuid().ToString();
                        } while (!Common.IsValidFileName(Guid.NewGuid().ToString(), true));

                        string path1 = string.Format("{0}/{1}", Server.MapPath("~/Content/Uploads/import/excel"), fname + ".xls");
                        if (System.IO.File.Exists(path1))
                            System.IO.File.Delete(path1);

                        Imp.file.SaveAs(path1);
                        //string sqlConnectionString = @"Data Source=LEEDHAR2-PC\SQLEXPRESS;Database=Leedhar_Import;Trusted_Connection=true;Persist Security Info=True";


                        //Create connection string to Excel work book
                        //string excelConnectionString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + path1 + ";Extended Properties=Excel 12.0;Persist Security Info=False";
                        string excelConnectionString = System.Configuration.ConfigurationManager.AppSettings["importexcel"].ToString().Replace("$#####$", path1);
                        //string excelConnectionString = @"Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + path1 + ";Extended Properties=\"\"Excel 8.0;HDR=YES;IMEX=1\"\"";

                        //Create Connection to Excel work book
                        OleDbConnection excelConnection = new OleDbConnection(excelConnectionString);

                        excelConnection.Open();

                        DataTable dt = new DataTable();

                        dt = excelConnection.GetSchema("Tables");
                        DataRow dr;
                        dr = dt.Rows[0];
                        string strsheetName = dr["Table_Name"].ToString().Replace("'", "");
                        excelConnection.Close();

                        //
                        if (strsheetName.ToLower() != "importusers$" && strsheetName.ToLower() != "sheet1$" && strsheetName.ToLower() != "exportuserssheet$")
                        {
                            ModelState.AddModelError("file", LMSResourse.Admin.User.msgInvalidSheetName);//BlenderResources.UserManagementResx.UserManagement.MsgImpFmtFile);
                        }
                        else
                        {
                            DataSet dsUsers = new DataSet();

                            OleDbDataAdapter da;
                            if (strsheetName.ToLower() == "importusers$")
                            {
                                da = new OleDbDataAdapter(String.Concat("Select * from [importusers$]"), excelConnection);
                                da.Fill(dsUsers, "Users");
                            }
                            else if (strsheetName.ToLower() == "exportuserssheet$")
                            {
                                da = new OleDbDataAdapter(String.Concat("Select * from [exportuserssheet$]"), excelConnection);
                                da.Fill(dsUsers, "Users");
                            }
                            else if (strsheetName.ToLower() == "sheet1$")
                            {
                                da = new OleDbDataAdapter(String.Concat("Select * from [sheet1$]"), excelConnection);
                                da.Fill(dsUsers, "Users");
                            }
                            #endregion

                            #region // Find Mandatory Columns in excel file
                            var defaultLanguage = db.InstanceInfoes.Find(1).DefaultLanguage;


                            var dtUserProfile = from UProfile in db.UserProfileSettings
                                                join usSetOrg in db.UserProfileSettingsOrgs on UProfile.ProfileSettingID equals usSetOrg.ProfileSettingID
                                                join usSetOrgInfo in db.UserProfileSettingsInfoes on usSetOrg.ProfileSettignOrgId equals usSetOrgInfo.ProfileSettignOrgId
                                                where usSetOrgInfo.LanguageID == defaultLanguage && usSetOrg.OrganisationID == Org.OrganisationID
                                                select new { usSetOrg.ProfileSettignOrgId, usSetOrg.ProfileSettingID, usSetOrgInfo.ProfileTitle, usSetOrg.IsDisplay, usSetOrg.IsMandatory, usSetOrg.MaxLength, usSetOrg.ProfileType };


                            string mandatoryColumns = "[FirstName *],[LastName *],[Email *],[IsActive *]";
                            string columns = "[FirstName *],[LastName *],[Email *],[IsActive *]";

                            string columnslength = "100,100,250,1";
                            string columnnotexist = "";

                            foreach (var us in dtUserProfile)
                            {
                                if (us.IsDisplay == true)
                                {
                                    if (us.IsMandatory == true)
                                    {
                                        columns += ",[" + us.ProfileTitle.TrimEnd().TrimStart() + " *" + "]";
                                        mandatoryColumns += ",[" + us.ProfileTitle.TrimEnd().TrimStart() + " *" + "]";
                                    }
                                    else
                                        columns += ",[" + us.ProfileTitle.TrimEnd().TrimStart() + "]";
                                    columnslength += "," + us.MaxLength.ToString();

                                }
                            }
                            string[] Allcolumns = columns.Split(',').ToArray();
                            string[] ALLMandatoryColumns = mandatoryColumns.Split(',').ToArray();
                            string[] AllColumnsLength = columnslength.Split(',').ToArray();

                            foreach (var col in Allcolumns)
                            {
                                if (!dsUsers.Tables[0].Columns.Contains(col.Replace("]", "").Replace("[", "")))
                                    columnnotexist = ((columnnotexist.Length > 0) ? columnnotexist + ", " + col : columnnotexist + col);
                            }
                            if (columnnotexist.Length > 0)
                            {
                                throw new Exception(columnnotexist + LMSResourse.Admin.User.msgImpReqCol);
                            }
                            #endregion

                            dtTempUsers = dsUsers.Tables["Users"];
                            dtTempUsers.Columns.Add("Status/Exception", "String".GetType());
                            //dtTempUsers.Columns.Add("Record #", System.Type.GetType("System.Int32"));
                            Int32 recordno = 0;

                            var duplicateUserNames = from b in dtTempUsers.AsEnumerable()
                                                     where b.Field<string>("Email *") != null
                                                     group b by b.Field<string>("Email *").ToLower().TrimStart().TrimEnd() into g
                                                     let count = g.Count()
                                                     where count > 1
                                                     select g.Key;
                            var dtUserProfileSettingsOrgId = dtUserProfile.Select(x => x.ProfileSettignOrgId).ToArray();
                            var GroupAdminValuesTemp = from x in db.UserProfileSettingsOrgValues
                                                       where dtUserProfileSettingsOrgId.Contains(x.ProfileSettignOrgId)
                                                       select x;
                            #region Checking  Organization


                            var orgusercountflag = true;
                            var CurrOrgUserCount = db.UserProfiles.Where(usr => usr.OrganisationID == Org.OrganisationID).Count();



                            #endregion



                            if (orgusercountflag == true)
                            {

                                var GroupAdminGroups = db.UserGroups.Where(x => x.UserId == 0).Select(x => x.GroupID).ToList();

                                foreach (DataRow UserRow in dtTempUsers.Rows)
                                {
                                    recordno++;
                                    //UserRow["Record #"] = recordno;
                                    string ErrorMessage = "";
                                    bool UserExist = false;
                                    bool duplicateEmailId = false;
                                    string aspNetUserId = "";
                                    long UserId = 0;
                                    string emailid = "";
                                    var tagWithoutClosingRegex = new Regex(@"<[^>]+>");
                                    var htmlerrormessage = LMSResourse.Common.Common.msgNoHTML;

                                    if (!string.IsNullOrWhiteSpace(Convert.ToString((UserRow["Email *"]))))
                                    {
                                        PostedUserRolesLocal UserRoleIds = new PostedUserRolesLocal();
                                        int[] postedgroups = { };

                                        #region // check User exist or not

                                        string username = Convert.ToString(UserRow["Email *"]).TrimStart().TrimEnd();
                                        emailid = Convert.ToString(UserRow["Email *"]).ToLower();

                                        if (duplicateUserNames.ToList().Contains(username.ToLower()))
                                        {
                                            ErrorMessage += LMSResourse.Admin.User.msgImpDupUserName + Environment.NewLine;
                                        }
                                        var FindUser = UserManager.FindByName(UserRow["Email *"].ToString().TrimEnd().TrimStart());

                                        if (FindUser != null)
                                        {
                                            aspNetUserId = FindUser.Id;

                                            var usProfile = db.UserProfiles.Where(x => x.Id == aspNetUserId).FirstOrDefault();
                                            if (usProfile != null)
                                                if (!usProfile.IsDelete)
                                                {
                                                    UserExist = true;
                                                    UserId = usProfile.UserId;
                                                }
                                        }
                                        #endregion

                                        if (Convert.ToString(UserRow["Email *"]).TrimEnd().TrimStart().Length > int.Parse(Convert.ToString(AllColumnsLength[2])))
                                            ErrorMessage += LMSResourse.Admin.User.msgMxlEmail + Environment.NewLine;
                                        else if (!IsValidEmailAddress(Convert.ToString(UserRow["Email *"]).TrimEnd()))
                                            ErrorMessage += LMSResourse.Admin.User.msgInvalidEmail + Environment.NewLine;
                                        if (string.IsNullOrWhiteSpace(ErrorMessage))
                                        {
                                            #region // validation of Firstname, last name email id
                                            if (string.IsNullOrEmpty(Convert.ToString(UserRow["FirstName *"])))
                                                ErrorMessage += LMSResourse.Admin.User.msgImpReqFirstName + Environment.NewLine;
                                            if (tagWithoutClosingRegex.IsMatch(Convert.ToString(UserRow["FirstName *"])))
                                                ErrorMessage += LMSResourse.Admin.User.fldFirstName + ": " + htmlerrormessage;

                                            if (string.IsNullOrEmpty(Convert.ToString(UserRow["LastName *"])))
                                                ErrorMessage += LMSResourse.Admin.User.msgImpReqLastName + Environment.NewLine;
                                            if (tagWithoutClosingRegex.IsMatch(Convert.ToString(UserRow["LastName *"])))
                                                ErrorMessage += LMSResourse.Admin.User.fldLastName + ": " + htmlerrormessage;
                                            if (string.IsNullOrEmpty(Convert.ToString(UserRow["IsActive *"])))
                                                ErrorMessage += LMSResourse.Admin.User.msgImpIsActive + Environment.NewLine;
                                            if (tagWithoutClosingRegex.IsMatch(Convert.ToString(UserRow["IsActive *"])))
                                                ErrorMessage += LMSResourse.Admin.User.fldStatus + ": " + htmlerrormessage;
                                            #endregion

                                            #region // check Organisation

                                            if (Convert.ToString(UserRow["Organisation"]).ToLower().Trim() != Org.OrganisationName.ToLower())
                                                ErrorMessage += LMSResourse.Admin.User.msgImpInvalidOrganisation + Environment.NewLine;

                                            #endregion

                                            #region // check Role section

                                            if (string.IsNullOrEmpty(Convert.ToString(UserRow["Role *"])))
                                                ErrorMessage += LMSResourse.Admin.User.fldRoles + ": " + LMSResourse.Admin.User.msgImpRole;

                                            // if error message is blank then check fro user role section
                                            if (string.IsNullOrWhiteSpace(ErrorMessage))
                                            {
                                                string[] UserRoles = Convert.ToString(UserRow["Role *"]).Split(',');
                                                for (int x = 0; x < UserRoles.Length; x++)
                                                    UserRoles[x] = UserRoles[x].ToLower().TrimStart().TrimEnd();
                                                var IsValidRoleExist = false;
                                                foreach (string x in UserRoles)
                                                {
                                                    if (AllAvailableRoles.Contains(Convert.ToString(x).ToLower().TrimEnd().TrimStart()))
                                                        IsValidRoleExist = true;
                                                    else
                                                        ErrorMessage += Convert.ToString(x).ToLower().TrimEnd().TrimStart() + ": " + LMSResourse.Admin.User.msgImpInvalidRole;
                                                }
                                                if (!IsValidRoleExist)
                                                    ErrorMessage += LMSResourse.Admin.User.msgImpInvalidRole;

                                                var Y = from x in db.AspNetRoles
                                                        where UserRoles.Contains(x.Name.ToLower().TrimEnd().TrimStart())
                                                        select x.Id;

                                                UserRoleIds.UserRolesLocalIds = Y.ToArray();
                                            }
                                            #endregion


                                            #region // check Group section

                                            if (string.IsNullOrEmpty(Convert.ToString(UserRow["Group *"])))
                                                ErrorMessage += LMSResourse.Admin.User.fldGroups + ": " + LMSResourse.Admin.User.msgImpGroup;

                                            string[] UserGroups = Convert.ToString(UserRow["Group *"]).Split(',');
                                            var IsValidGroupExist = false;
                                            foreach (string x in UserGroups)
                                            {
                                                if (AllAvailableGroups.Contains(Convert.ToString(x).ToLower().TrimEnd().TrimStart()))
                                                    IsValidGroupExist = true;
                                                else
                                                    ErrorMessage += Convert.ToString(x).ToLower().TrimEnd().TrimStart() + ": " + LMSResourse.Admin.User.msgImpInvalidGroup;
                                            }
                                            if (IsValidGroupExist)
                                            {
                                                postedgroups = db.Groups.Where(x => UserGroups.Contains(x.GroupName.ToLower().TrimEnd().TrimStart())).Select(x => x.GroupID).ToArray();
                                            }


                                            #endregion



                                            #region // Checking Max user allowed for the current user
                                            Boolean willAdd = true;
                                            List<int> groupIdLi = new List<int>();
                                            if (postedgroups != null)
                                                foreach (var grp in postedgroups)
                                                {
                                                    var tempGroupId = Convert.ToInt32(grp.ToString().Split('~')[0].ToString());
                                                    if (tempGroupId > 0)
                                                    {
                                                        groupIdLi.Add(tempGroupId);
                                                        var groupDetail = db.Groups.Where(a => a.GroupID == tempGroupId).FirstOrDefault();
                                                        if (groupDetail.MaxUsers > (UserExist ? groupDetail.UserGroups.Where(a => a.UserId != UserId).Count() : groupDetail.UserGroups.ToList().Count()))
                                                        { }
                                                        else
                                                        {
                                                            willAdd = false;
                                                            ErrorMessage = !UserExist ? LMSResourse.Admin.User.msgMaxUserExceed : LMSResourse.Admin.User.msgUpdateMaxUserExceed;
                                                        }
                                                    }
                                                }
                                            #endregion


                                            if (ErrorMessage == "")
                                            {
                                                if (UserExist && willAdd)
                                                {
                                                    #region // User updation
                                                    var usProfile = db.UserProfiles.Where(x => x.Id == aspNetUserId).FirstOrDefault();
                                                    #region // Is Group admin

                                                    if (IsGroupAdmin)
                                                    {
                                                        GroupAdminGroups = db.UserGroups.Where(x => x.UserId == currentLoginUser).Select(x => x.GroupID).ToList();
                                                        var GroupAdminUsers = db.UserGroups.Where(x => GroupAdminGroups.Contains(x.GroupID)).Select(x => x.UserId).Distinct().ToArray();
                                                        if (!GroupAdminUsers.Contains(UserId)) // if admin is editing an invalid user then redirect to index page.
                                                        {
                                                            ErrorMessage += "Not have sufficient rights to change the data.";
                                                        }
                                                    }
                                                    #endregion

                                                    if (ErrorMessage.Length == 0)
                                                    {
                                                        usProfile.FirstName = (Convert.ToString(UserRow["FirstName *"]).Length > int.Parse(Convert.ToString(AllColumnsLength[0]))) ? Convert.ToString(UserRow["FirstName *"]).Substring(0, int.Parse(Convert.ToString(AllColumnsLength[0]))) : Convert.ToString(UserRow["FirstName *"]);
                                                        usProfile.LastName = (Convert.ToString(UserRow["LastName *"]).Length > int.Parse(Convert.ToString(AllColumnsLength[1]))) ? Convert.ToString(UserRow["LastName *"]).Substring(0, int.Parse(Convert.ToString(AllColumnsLength[0]))) : Convert.ToString(UserRow["LastName *"]);
                                                        usProfile.Status = (Convert.ToString(UserRow["IsActive *"]).Length >= int.Parse(Convert.ToString(AllColumnsLength[3]))) ? (Convert.ToString(UserRow["IsActive *"]).Trim().ToUpper() == "Y") ? true : false : false;

                                                        var userprofilevalues = dtUserProfile.ToList();
                                                        #region // Update Profile section

                                                        foreach (var us in dtUserProfile)
                                                        {
                                                            string ismandatory = "";
                                                            if (us.IsDisplay == true)
                                                            {
                                                                if (us.IsMandatory == true)
                                                                {
                                                                    if (string.IsNullOrEmpty(Convert.ToString(UserRow[us.ProfileTitle.ToString() + " *"])))
                                                                        ErrorMessage += us.ProfileTitle.ToString() + LMSResourse.Admin.User.msgImpFldMissing + Environment.NewLine;
                                                                    ismandatory = " *";
                                                                }

                                                                string uservalue = "";
                                                                switch (us.ProfileSettingID)
                                                                {


                                                                    case 1:
                                                                        uservalue = (Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Length > us.MaxLength) ? Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Substring(0, (int)us.MaxLength) : Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]);
                                                                        if (tagWithoutClosingRegex.IsMatch(Convert.ToString(uservalue)))
                                                                            ErrorMessage += us.ProfileTitle + ismandatory + ": " + htmlerrormessage;
                                                                        if (us.ProfileType == 1 && !string.IsNullOrWhiteSpace(Convert.ToString(UserRow[us.ProfileTitle + ismandatory])))
                                                                        {
                                                                            usProfile.Option1 = (Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Length > us.MaxLength ? Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Substring(0, us.MaxLength) : Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]));
                                                                        }
                                                                        else if (us.ProfileType == 2 && !string.IsNullOrWhiteSpace(Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory])))
                                                                        {
                                                                            var testvalues = from profilevalues in db.UserProfileSettingsOrgValues
                                                                                             join settingOrg in db.UserProfileSettingsOrgs on profilevalues.ProfileSettignOrgId equals settingOrg.ProfileSettignOrgId
                                                                                             where settingOrg.OrganisationID == Org.OrganisationID && settingOrg.ProfileSettingID == 1 &&
                                                                                             profilevalues.ProfileValuesTitle.ToLower().TrimEnd().TrimStart() == uservalue.ToLower().TrimEnd().TrimStart()
                                                                                             select profilevalues;
                                                                            if (testvalues.Count() == 0)
                                                                            {
                                                                                ErrorMessage += us.ProfileTitle.TrimEnd().TrimStart() + ismandatory + LMSResourse.Admin.User.msgImpFmt;
                                                                            }
                                                                            else
                                                                                usProfile.Option1 = testvalues.ToList()[0].ProfileValuesTitle;
                                                                        }
                                                                        else
                                                                            usProfile.Option1 = Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]);
                                                                        break;
                                                                    case 2:
                                                                        uservalue = (Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Length > us.MaxLength) ? Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Substring(0, (int)us.MaxLength) : Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]);
                                                                        if (tagWithoutClosingRegex.IsMatch(Convert.ToString(uservalue)))
                                                                            ErrorMessage += us.ProfileTitle.TrimEnd().TrimStart() + ismandatory + ": " + htmlerrormessage;
                                                                        if (us.ProfileType == 1 && !string.IsNullOrWhiteSpace(Convert.ToString(UserRow[us.ProfileTitle + ismandatory])))
                                                                        {
                                                                            usProfile.Option2 = (Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Length > us.MaxLength ? Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Substring(0, us.MaxLength) : Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]));
                                                                        }
                                                                        else if (us.ProfileType == 2 && !string.IsNullOrWhiteSpace(Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory])))
                                                                        {
                                                                            var testvalues = from profilevalues in db.UserProfileSettingsOrgValues
                                                                                             join settingOrg in db.UserProfileSettingsOrgs on profilevalues.ProfileSettignOrgId equals settingOrg.ProfileSettignOrgId
                                                                                             where settingOrg.OrganisationID == Org.OrganisationID && settingOrg.ProfileSettingID == 2 &&
                                                                                             profilevalues.ProfileValuesTitle.ToLower().TrimEnd().TrimStart() == uservalue.ToLower().TrimEnd().TrimStart()
                                                                                             select profilevalues;
                                                                            if (testvalues.Count() == 0)
                                                                            {
                                                                                ErrorMessage += us.ProfileTitle.TrimEnd().TrimStart() + ismandatory + LMSResourse.Admin.User.msgImpFmt;
                                                                            }
                                                                            else
                                                                                usProfile.Option2 = testvalues.ToList()[0].ProfileValuesTitle;
                                                                        }
                                                                        else
                                                                            usProfile.Option2 = Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]);
                                                                        break;
                                                                }
                                                            }
                                                        }
                                                        #endregion
                                                    }
                                                    #endregion
                                                    if (ErrorMessage != "")
                                                    {
                                                        UserRow["Status/Exception"] = ErrorMessage;
                                                        Imp.RecordFailed++;
                                                        usProfile = null;
                                                    }
                                                    else
                                                    {
                                                        usProfile.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                                                        usProfile.DateLastModified = DateTime.Now;
                                                        try
                                                        {
                                                            db.Entry(usProfile).State = EntityState.Modified;
                                                            db.SaveChanges();
                                                            #region // update User Role
                                                            if ((IsGroupAdmin || IsSuperAdmin) && usProfile.UserId != currentLoginUser) // group adminstrator can change it's role.
                                                                SetRolesToUser(usProfile.EmailAddress, UserRoleIds);
                                                            #endregion
                                                            #region // group change update User Groups



                                                            // Get the groups already assigned
                                                            // if current user is group administrator then only the assigned and group admin groups will come in assigned grpups.
                                                            int[] assignedGroups = db.UserGroups.Where(x => x.UserId == usProfile.UserId && (IsGroupAdmin ? GroupAdminGroups.Contains(x.GroupID) : 0 == 0)).Select(x => x.GroupID).ToArray();
                                                            // Get the groups which have to assigned according to selection on webpage
                                                            var GroupsToAssigne = postedgroups.Except(assignedGroups);

                                                            foreach (var y in GroupsToAssigne)
                                                            {
                                                                UserGroup ObjUs;
                                                                ObjUs = db.UserGroups.Where(x => x.GroupID == y && x.UserId == usProfile.UserId).FirstOrDefault();
                                                                if (ObjUs == null)
                                                                {
                                                                    ObjUs = new UserGroup();
                                                                    ObjUs.UserId = usProfile.UserId;
                                                                    ObjUs.GroupID = y;
                                                                    ObjUs.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                                                                    ObjUs.DateLastModified = DateTime.Now;
                                                                    db.UserGroups.Add(ObjUs);
                                                                    db.SaveChanges();
                                                                }
                                                            }

                                                            // remove assigned groups which are not checked or unchecked.
                                                            // refresh the assigned groups
                                                            assignedGroups = db.UserGroups.Where(x => x.UserId == usProfile.UserId && (IsGroupAdmin ? GroupAdminGroups.Contains(x.GroupID) : 0 == 0)).Select(x => x.GroupID).ToArray();
                                                            // Get the groups which have to unassign according to selection on webpage
                                                            var GroupsToRemove = assignedGroups.Except(postedgroups);

                                                            var ObjGroupsToRemove = (from x in db.UserGroups
                                                                                     where x.UserId == usProfile.UserId && GroupsToRemove.Contains(x.GroupID)
                                                                                     orderby x.UserId, x.GroupID
                                                                                     select x).ToList();
                                                            foreach (var x in ObjGroupsToRemove)
                                                            {
                                                                var objremove = db.UserGroups.Find(x.UserGroupId);
                                                                db.UserGroups.Remove(objremove);
                                                                db.SaveChanges();
                                                            }
                                                            #endregion
                                                            UserRow["Status/Exception"] = LMSResourse.Admin.User.msgImpRecStatusUp + Environment.NewLine;
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            UserRow["Status/Exception"] = LMSResourse.Admin.User.msgImpRecStatusErr + Environment.NewLine + ex.ToString();
                                                        }
                                                        finally
                                                        {
                                                            usProfile = null;
                                                        }
                                                        Imp.RecordUpdated++;
                                                    }
                                                }
                                                else
                                                {
                                                    #region // User Creation
                                                    UserProfile usProfile = new UserProfile();
                                                    if (ErrorMessage.Length == 0)
                                                    {
                                                        usProfile.EmailAddress = Convert.ToString(UserRow["Email *"]).TrimEnd().TrimStart();
                                                        usProfile.FirstName = (Convert.ToString(UserRow["FirstName *"]).Length > int.Parse(Convert.ToString(AllColumnsLength[0]))) ? Convert.ToString(UserRow["FirstName *"]).Substring(0, int.Parse(Convert.ToString(AllColumnsLength[0]))) : Convert.ToString(UserRow["FirstName *"]);
                                                        usProfile.LastName = (Convert.ToString(UserRow["LastName *"]).Length > int.Parse(Convert.ToString(AllColumnsLength[1]))) ? Convert.ToString(UserRow["LastName *"]).Substring(0, int.Parse(Convert.ToString(AllColumnsLength[0]))) : Convert.ToString(UserRow["LastName *"]);
                                                        usProfile.Status = (Convert.ToString(UserRow["IsActive *"]).Length >= int.Parse(Convert.ToString(AllColumnsLength[3]))) ? (Convert.ToString(UserRow["IsActive *"]).Trim().ToUpper() == "Y") ? true : false : false;

                                                        var userprofilevalues = dtUserProfile.ToList();
                                                        #region // Update Profile section

                                                        foreach (var us in dtUserProfile)
                                                        {
                                                            string ismandatory = "";
                                                            if (us.IsDisplay == true)
                                                            {
                                                                if (us.IsMandatory == true)
                                                                {
                                                                    if (string.IsNullOrEmpty(Convert.ToString(UserRow[us.ProfileTitle.ToString() + " *"])))
                                                                        ErrorMessage += us.ProfileTitle.ToString() + LMSResourse.Admin.User.msgImpFldMissing + Environment.NewLine;
                                                                    ismandatory = " *";
                                                                }

                                                                string uservalue = "";
                                                                switch (us.ProfileSettingID)
                                                                {


                                                                    case 1:
                                                                        uservalue = (Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Length > us.MaxLength) ? Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Substring(0, (int)us.MaxLength) : Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]);
                                                                        if (tagWithoutClosingRegex.IsMatch(Convert.ToString(uservalue)))
                                                                            ErrorMessage += us.ProfileTitle + ismandatory + ": " + htmlerrormessage;
                                                                        if (us.ProfileType == 1 && !string.IsNullOrWhiteSpace(Convert.ToString(UserRow[us.ProfileTitle + ismandatory])))
                                                                        {
                                                                            usProfile.Option1 = (Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Length > us.MaxLength ? Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Substring(0, us.MaxLength) : Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]));
                                                                        }
                                                                        else if (us.ProfileType == 2 && !string.IsNullOrWhiteSpace(Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory])))
                                                                        {
                                                                            var testvalues = from profilevalues in db.UserProfileSettingsOrgValues
                                                                                             join settingOrg in db.UserProfileSettingsOrgs on profilevalues.ProfileSettignOrgId equals settingOrg.ProfileSettignOrgId
                                                                                             where settingOrg.OrganisationID == Org.OrganisationID && settingOrg.ProfileSettingID == 1 &&
                                                                                             profilevalues.ProfileValuesTitle.ToLower().TrimEnd().TrimStart() == uservalue.ToLower().TrimEnd().TrimStart()
                                                                                             select profilevalues;
                                                                            if (testvalues.Count() == 0)
                                                                            {
                                                                                ErrorMessage += us.ProfileTitle.TrimEnd().TrimStart() + ismandatory + LMSResourse.Admin.User.msgImpFmt;
                                                                            }
                                                                            else
                                                                                usProfile.Option1 = testvalues.ToList()[0].ProfileValuesTitle;
                                                                        }
                                                                        else
                                                                            usProfile.Option1 = Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]);
                                                                        break;
                                                                    case 2:
                                                                        uservalue = (Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Length > us.MaxLength) ? Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Substring(0, (int)us.MaxLength) : Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]);
                                                                        if (tagWithoutClosingRegex.IsMatch(Convert.ToString(uservalue)))
                                                                            ErrorMessage += us.ProfileTitle.TrimEnd().TrimStart() + ismandatory + ": " + htmlerrormessage;
                                                                        if (us.ProfileType == 1 && !string.IsNullOrWhiteSpace(Convert.ToString(UserRow[us.ProfileTitle + ismandatory])))
                                                                        {
                                                                            usProfile.Option2 = (Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Length > us.MaxLength ? Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]).Substring(0, us.MaxLength) : Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]));
                                                                        }
                                                                        else if (us.ProfileType == 2 && !string.IsNullOrWhiteSpace(Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory])))
                                                                        {
                                                                            var testvalues = from profilevalues in db.UserProfileSettingsOrgValues
                                                                                             join settingOrg in db.UserProfileSettingsOrgs on profilevalues.ProfileSettignOrgId equals settingOrg.ProfileSettignOrgId
                                                                                             where settingOrg.OrganisationID == Org.OrganisationID && settingOrg.ProfileSettingID == 2 &&
                                                                                             profilevalues.ProfileValuesTitle.ToLower().TrimEnd().TrimStart() == uservalue.ToLower().TrimEnd().TrimStart()
                                                                                             select profilevalues;
                                                                            if (testvalues.Count() == 0)
                                                                            {
                                                                                ErrorMessage += us.ProfileTitle.TrimEnd().TrimStart() + ismandatory + LMSResourse.Admin.User.msgImpFmt;
                                                                            }
                                                                            else
                                                                                usProfile.Option2 = testvalues.ToList()[0].ProfileValuesTitle;
                                                                        }
                                                                        else
                                                                            usProfile.Option2 = Convert.ToString(UserRow[us.ProfileTitle.TrimEnd().TrimStart() + ismandatory]);
                                                                        break;
                                                                }
                                                            }
                                                        }
                                                        #endregion
                                                        if (ErrorMessage != "")
                                                        {
                                                            UserRow["Status/Exception"] = ErrorMessage;
                                                            Imp.RecordFailed++;
                                                            usProfile = null;
                                                        }
                                                        else
                                                        {
                                                            usProfile.RegistrationDate = DateTime.Now;
                                                            usProfile.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                                                            usProfile.DateLastModified = DateTime.Now;
                                                            usProfile.LanguageId = db.InstanceInfoes.Find(1).DefaultLanguage;
                                                            usProfile.OrganisationID = Org.OrganisationID;
                                                            try
                                                            {
                                                                var user = new ApplicationUser() { UserName = usProfile.EmailAddress };
                                                                var password = System.Configuration.ConfigurationManager.AppSettings["defaultPassword"].ToString();//AjaxGeneratePassword();
                                                                var result = UserManager.Create(user, password);
                                                                if (result.Succeeded)
                                                                {
                                                                    usProfile.Id = user.Id;
                                                                    usProfile.IsDelete = false;
                                                                    /**Changes for setting default password and making user active by default*****/
                                                                    usProfile.EmployeeID = usProfile.EmailAddress;
                                                                    usProfile.Status = true;
                                                                    usProfile.IsRegisterMailSend = false;
                                                                    /*******/
                                                                    db.UserProfiles.Add(usProfile);
                                                                    db.SaveChanges();
                                                                    #region // update User Role
                                                                    if ((IsGroupAdmin || IsSuperAdmin) && usProfile.UserId != currentLoginUser) // group adminstrator can change it's role.
                                                                        SetRolesToUser(usProfile.EmailAddress, UserRoleIds);
                                                                    #endregion
                                                                    #region // group change update User Groups



                                                                    // Get the groups already assigned
                                                                    // if current user is group administrator then only the assigned and group admin groups will come in assigned grpups.
                                                                    int[] assignedGroups = db.UserGroups.Where(x => x.UserId == usProfile.UserId && (IsGroupAdmin ? GroupAdminGroups.Contains(x.GroupID) : 0 == 0)).Select(x => x.GroupID).ToArray();
                                                                    // Get the groups which have to assigned according to selection on webpage
                                                                    var GroupsToAssigne = postedgroups.Except(assignedGroups);

                                                                    foreach (var y in GroupsToAssigne)
                                                                    {
                                                                        UserGroup ObjUs;
                                                                        ObjUs = db.UserGroups.Where(x => x.GroupID == y && x.UserId == usProfile.UserId).FirstOrDefault();
                                                                        if (ObjUs == null)
                                                                        {
                                                                            ObjUs = new UserGroup();
                                                                            ObjUs.UserId = usProfile.UserId;
                                                                            ObjUs.GroupID = y;
                                                                            ObjUs.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                                                                            ObjUs.DateLastModified = DateTime.Now;
                                                                            db.UserGroups.Add(ObjUs);
                                                                            db.SaveChanges();
                                                                        }
                                                                    }

                                                                    // remove assigned groups which are not checked or unchecked.
                                                                    // refresh the assigned groups
                                                                    assignedGroups = db.UserGroups.Where(x => x.UserId == usProfile.UserId && (IsGroupAdmin ? GroupAdminGroups.Contains(x.GroupID) : 0 == 0)).Select(x => x.GroupID).ToArray();
                                                                    // Get the groups which have to unassign according to selection on webpage
                                                                    var GroupsToRemove = assignedGroups.Except(postedgroups);

                                                                    var ObjGroupsToRemove = (from x in db.UserGroups
                                                                                             where x.UserId == usProfile.UserId && GroupsToRemove.Contains(x.GroupID)
                                                                                             orderby x.UserId, x.GroupID
                                                                                             select x).ToList();
                                                                    foreach (var x in ObjGroupsToRemove)
                                                                    {
                                                                        var objremove = db.UserGroups.Find(x.UserGroupId);
                                                                        db.UserGroups.Remove(objremove);
                                                                        db.SaveChanges();
                                                                    }
                                                                    #endregion
                                                                    UserRow["Status/Exception"] = LMSResourse.Admin.User.msgImpRecStatusCr + Environment.NewLine;
                                                                    ResetPasswordandSendMail("REGU", usProfile.EmailAddress, password);
                                                                    usProfile.IsRegisterMailSend = false;
                                                                    Imp.RecordInserted++;
                                                                }
                                                                else
                                                                {
                                                                    var errorMessage = "";
                                                                    foreach (var x in result.Errors)
                                                                        errorMessage += x.ToString() + Environment.NewLine;
                                                                    UserRow["Status/Exception"] = errorMessage;
                                                                    Imp.RecordFailed++;
                                                                }

                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                UserRow["Status/Exception"] = LMSResourse.Admin.User.msgImpRecStatusErr + Environment.NewLine + ex.ToString();
                                                                Imp.RecordFailed++;
                                                            }
                                                            finally
                                                            {
                                                                usProfile = null;
                                                            }

                                                        }
                                                    }
                                                    #endregion
                                                }
                                            }
                                            else
                                            {
                                                UserRow["Status/Exception"] = ErrorMessage;
                                                Imp.RecordFailed++;
                                            }

                                        }
                                        else
                                        {
                                            UserRow["Status/Exception"] = ErrorMessage;
                                            Imp.RecordFailed++;
                                        }
                                    }
                                    else
                                    {
                                        UserRow["Status/Exception"] = LMSResourse.Admin.User.msgImpReqEmail;
                                        Imp.RecordFailed++;

                                    }
                                }
                            }
                            else  //Organisation user count
                            {
                                dtTempUsers = new DataTable();
                                ModelState.AddModelError("file", @LMSResourse.Admin.User.msgLicenceUserExceed);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("expected format"))
                            ModelState.AddModelError("file", LMSResourse.Admin.User.msgImpFmtFile);
                        else
                            ModelState.AddModelError("file", ex.Message.ToString());

                    }

                }
                Imp.Recordsreturned = dtTempUsers;
                ViewBag.TotalRecords = HttpUtility.HtmlEncode(Convert.ToString(Imp.RecordInserted + Imp.RecordUpdated));
                updateAssignedUserOfAllGroups();
                return View(Imp);
            }
            return View(Imp);
        }
        #endregion

        #region//Export User
        public void CreateUserTempleateWithData(int OrganisationId)
        {
            using (StringWriter sw = new StringWriter())
            {
                using (HtmlTextWriter htw = new System.Web.UI.HtmlTextWriter(sw))
                {
                    HtmlTable ProfileTable = new HtmlTable();

                    HtmlTableRow trProfileRow = new HtmlTableRow();
                    HtmlTableCell tdProfileCell = new HtmlTableCell();
                    Label lblUniqueIdentifier = new Label();

                    ProfileTable.Style.Add("border", "1");
                    ProfileTable.Style.Add("border-width", "1");
                    ProfileTable.Style.Add("border-color", "Black");

                    lblUniqueIdentifier.Text = " *";

                    var defaultLanguage = db.InstanceInfoes.Find(1).DefaultLanguage;
                    var Org = db.Organisations.Find(OrganisationId);

                    var dtUserProfile = from UProfile in db.UserProfileSettings
                                        join usSetOrg in db.UserProfileSettingsOrgs on UProfile.ProfileSettingID equals usSetOrg.ProfileSettingID
                                        join usSetOrgInfo in db.UserProfileSettingsInfoes on usSetOrg.ProfileSettignOrgId equals usSetOrgInfo.ProfileSettignOrgId
                                        where usSetOrgInfo.LanguageID == defaultLanguage && usSetOrg.OrganisationID == Org.OrganisationID
                                        orderby usSetOrg.ProfileSettingID
                                        select new { usSetOrg.ProfileSettingID, usSetOrgInfo.ProfileTitle, usSetOrg.IsDisplay, usSetOrg.IsMandatory };

                    var dtUserData = from us in db.UserProfiles
                                     where us.OrganisationID == ((OrganisationId == 0) ? us.OrganisationID : OrganisationId) // ( us.OrganisationID)
                                     && us.IsDelete == false
                                     select us;

                    // UserName
                    //tdProfileCell = new HtmlTableCell();
                    //tdProfileCell.InnerText = "UserName";
                    //tdProfileCell.Width = "150";
                    //tdProfileCell.Style.Add("font-family", "Arial");
                    //tdProfileCell.Style.Add("font-size", "12px");
                    //tdProfileCell.Style.Add("font-weight", "bold");
                    //tdProfileCell.Style.Add("background", "#999999");
                    //lblUniqueIdentifier = new Label();

                    //lblUniqueIdentifier.Text = " *";
                    //tdProfileCell.Controls.Add(lblUniqueIdentifier);
                    //trProfileRow.Cells.Add(tdProfileCell);


                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "FirstName";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);


                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "LastName";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();
                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);
                    trProfileRow.Cells.Add(tdProfileCell);


                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "Email";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "IsActive";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();
                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "Role";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "Organisation";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    //lblUniqueIdentifier.Text = " *";
                    //tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    tdProfileCell = new HtmlTableCell();
                    tdProfileCell.InnerText = "Group";
                    tdProfileCell.Width = "150";
                    tdProfileCell.Style.Add("font-family", "Arial");
                    tdProfileCell.Style.Add("font-size", "12px");
                    tdProfileCell.Style.Add("font-weight", "bold");
                    tdProfileCell.Style.Add("background", "#999999");
                    lblUniqueIdentifier = new Label();

                    lblUniqueIdentifier.Text = " *";
                    tdProfileCell.Controls.Add(lblUniqueIdentifier);

                    trProfileRow.Cells.Add(tdProfileCell);

                    foreach (var us in dtUserProfile.ToList())
                    {
                        if (us.IsDisplay == true)
                        {

                            tdProfileCell = new HtmlTableCell();
                            tdProfileCell.InnerText = us.ProfileTitle.ToString().TrimEnd().TrimStart();
                            tdProfileCell.Width = "150";
                            tdProfileCell.Style.Add("font-family", "Arial");
                            tdProfileCell.Style.Add("font-size", "12px");
                            tdProfileCell.Style.Add("font-weight", "bold");
                            tdProfileCell.Style.Add("background", "#999999");
                            if (us.IsMandatory == true)
                            {
                                lblUniqueIdentifier = new Label();

                                lblUniqueIdentifier.Text = " *";
                                tdProfileCell.Controls.Add(lblUniqueIdentifier);
                            }


                            trProfileRow.Cells.Add(tdProfileCell);

                        }
                    }
                    ProfileTable.Rows.Add(trProfileRow);


                    ProfileTable.Rows.Add(trProfileRow);

                    foreach (var x in dtUserData.ToList())
                    {
                        trProfileRow = new HtmlTableRow();

                        tdProfileCell = new HtmlTableCell();
                        tdProfileCell.InnerText = x.FirstName;
                        trProfileRow.Cells.Add(tdProfileCell);

                        tdProfileCell = new HtmlTableCell();
                        tdProfileCell.InnerText = x.LastName;
                        trProfileRow.Cells.Add(tdProfileCell);

                        tdProfileCell = new HtmlTableCell();
                        tdProfileCell.InnerText = x.EmailAddress;
                        trProfileRow.Cells.Add(tdProfileCell);

                        tdProfileCell = new HtmlTableCell();
                        tdProfileCell.InnerText = (x.Status) ? "Y" : "N";
                        trProfileRow.Cells.Add(tdProfileCell);

                        tdProfileCell = new HtmlTableCell();
                        tdProfileCell.InnerText = string.Join(", ", x.AspNetUser.AspNetUserRoles.Select(y => y.AspNetRole.Name).ToArray());
                        trProfileRow.Cells.Add(tdProfileCell);

                        tdProfileCell = new HtmlTableCell();
                        tdProfileCell.InnerText = x.Organisation.OrganisationName;
                        trProfileRow.Cells.Add(tdProfileCell);

                        tdProfileCell = new HtmlTableCell();
                        tdProfileCell.InnerText = string.Join(", ", x.UserGroups.Select(y => y.Group.GroupName).ToArray());
                        trProfileRow.Cells.Add(tdProfileCell);

                        foreach (var us in dtUserProfile.ToList())
                        {
                            if (us.IsDisplay == true)
                            {

                                tdProfileCell = new HtmlTableCell();
                                switch (us.ProfileSettingID)
                                {
                                    case 1:
                                        tdProfileCell.InnerText = x.Option1;
                                        break;
                                    case 2:
                                        tdProfileCell.InnerText = x.Option2;
                                        break;
                                    default:
                                        tdProfileCell.InnerText = "";
                                        break;
                                }
                                trProfileRow.Cells.Add(tdProfileCell);
                            }
                        }
                        ProfileTable.Rows.Add(trProfileRow);
                    }


                    Response.ContentType = "application/ms-excel";
                    Response.AddHeader("content-disposition", "attachment; filename=ImportUsers.xls;");


                    ProfileTable.RenderControl(htw);
                    Response.Write(sw.ToString());
                    Response.End();
                }
            }
        }
        #endregion
        private static bool IsValidEmailAddress(string emailAddress)
        {
            return new System.ComponentModel.DataAnnotations
                                .EmailAddressAttribute()
                                .IsValid(emailAddress);
        }

        #region Update Assigned user of all groups
        private void updateAssignedUserOfAllGroups()
        {
            var allGroups = db.Groups.Where(a => a.Status == true && a.IsDeleted == false && a.OrganisationID != null).ToList();
            foreach (var groupDetail in allGroups)
            {
                if (groupDetail != null)
                {
                    var tep = groupDetail.UserGroups;
                    groupDetail.AssignedUsers = tep.Where(a => a.UserProfile.IsDelete == false && a.UserProfile.Status == true).Count();
                    db.SaveChanges();
                }

            }

        }
        #endregion
    }
}
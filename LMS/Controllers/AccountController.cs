using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Owin.Security;
using LMS.Models;
using CLSLms;
using System.Data.SqlClient;
using System.Configuration;
using System.Text;
using System.Web.Script.Serialization;
using Stripe;
using System.Data.Entity.Validation;
using Spire.Doc;


namespace LMS.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private LeopinkLMSDBEntities db = new LeopinkLMSDBEntities();
        public AccountController()
            : this(new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext())))
        {
            UserManager.UserValidator = new UserValidator<ApplicationUser>(UserManager) { AllowOnlyAlphanumericUserNames = false };
        }

        public AccountController(UserManager<ApplicationUser> userManager)
        {
            UserManager = userManager;
        }

        public UserManager<ApplicationUser> UserManager { get; private set; }




        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (Session["UserID"] != null)
            {
                return RedirectToLocal(returnUrl);
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                var userProfile = db.UserProfiles.Where(x => x.EmailAddress == model.UserName && x.IsDelete == false && x.Status == true).FirstOrDefault();
                var user = await UserManager.FindAsync(model.UserName, model.Password);
                if (user != null && userProfile != null)
                {

                    {
                        await SignInAsync(user, model.RememberMe);
                        List<MAclActions> oUserInRoles = db.Database.SqlQuery<MAclActions>("SELECT AM.ACLActionID,ActionFQN,UR.RoleId FROM ACLMatrix AS AM INNER JOIN AspNetUserRoles AS UR ON AM.RoleID=UR.RoleId INNER JOIN ACLActions AS ACLS ON ACLS.ACLActionID=AM.ACLActionID WHERE AM.IsAccess = 1 and UR.UserId IN(SELECT AspNetUsers.Id FROM AspNetUsers WHERE Username=@Username)", new SqlParameter("Username", model.UserName)).ToList();
                        //var oUserInRoles = db.Database.SqlQuery<MAclActions>("SELECT AM.ACLActionID,ActionFQN,UR.RoleId FROM ACLMatrix AS AM INNER JOIN AspNetUserRoles AS UR ON AM.RoleID=UR.RoleId INNER JOIN ACLActions AS ACLS ON ACLS.ACLActionID=AM.ACLActionID WHERE AM.IsAccess = 1 and UR.UserId IN(SELECT UserId FROM AspNetUsers WHERE Username=@Username)", new SqlParameter("Username", model.UserName)).ToList();

                        Session["MAclActions"] = oUserInRoles;
                        // No need to get the user detail again as the details can be get from userprofile obj.
                        //var currentUser = db.UserProfiles.Where(x => x.Id == user.Id).FirstOrDefault();
                        var currentUser = userProfile;
                        if (currentUser.IsRegisterMailSend == true)
                        {
                            Session["MAclActions"] = null;
                            RegisterViewModel re = new RegisterViewModel();
                            re.UserName = user.UserName;
                            AuthenticationManager.SignOut();
                            return RedirectToAction("Manage", "Account");
                        }
                        else
                        {
                            Session["UserID"] = currentUser.UserId;
                            Session["LastName"] = currentUser.LastName; //session used for scorm course API12.vb
                            Session["Firstname"] = currentUser.FirstName; //session used for scorm course API12.vb
                            Session["LanguageId"] = currentUser.LanguageId;
                            Session["IsGroupAdmin"] = UserManager.IsInRole(currentUser.Id, ConfigurationManager.AppSettings["GroupAdminRole"].ToString());
                            Session["UserRoles"] = GetUserRolesSession(UserManager.GetRoles(currentUser.Id));
                            Session["IsAdminView"] = ((Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2) || (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(1));
                            Session["EmployeeID"] = currentUser.EmailAddress;

                            if (UserManager.IsInRole(userProfile.Id, "Administrator"))
                                Session["IsSuperAdmin"] = true;
                            else
                                Session["IsSuperAdmin"] = false;

                            if (UserManager.IsInRole(userProfile.Id, "Course Admin"))
                            {
                                Session["CourseMangerRole"] = "CA";
                                Session["IsAdminView"] = true;
                            }
                            else if (UserManager.IsInRole(userProfile.Id, "Course Publisher"))
                            {
                                Session["CourseMangerRole"] = "CP";
                                Session["IsAdminView"] = true;
                            }
                            else if (UserManager.IsInRole(userProfile.Id, "Course Reviewer"))
                            {
                                Session["CourseMangerRole"] = "CR";
                                Session["IsAdminView"] = true;
                            }
                            else if (UserManager.IsInRole(userProfile.Id, "Course Creator"))
                            {
                                Session["CourseMangerRole"] = "CC";
                                Session["IsAdminView"] = true;
                            }
                            else
                            {
                                Session["CourseMangerRole"] = "NA";
                            }

                            if ((Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(8) || (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(9))
                            {
                                Session["IsParentTeacher"] = true;
                            }
                            else
                                Session["IsParentTeacher"] = false;

                            #region // Getting System Information like OS, Processor etc.
                            string ip = System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                            if (string.IsNullOrEmpty(ip))
                            {
                                ip = System.Web.HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
                            }
                            string OSversion = "Unknwon", Platform = "";
                            if (Request.UserAgent.IndexOf("Windows 95") > 0)
                            {
                                OSversion = "Windows 95";
                            }
                            else if (Request.UserAgent.IndexOf("Windows 98") > 0)
                            {
                                OSversion = "Windows 98";
                            }
                            else if (Request.UserAgent.IndexOf("Windows NT 5.0") > 0)
                            {
                                OSversion = "Windows 2000";
                            }
                            else if (Request.UserAgent.IndexOf("Windows NT 5.1") > 0)
                            {
                                OSversion = "XP";
                            }
                            else if (Request.UserAgent.IndexOf("Windows NT 5.2") > 0)
                            {
                                OSversion = "Windows Server 2003";
                            }
                            else if (Request.UserAgent.IndexOf("Windows NT 6.0") > 0)
                            {
                                OSversion = "VISTA";
                            }
                            else if (Request.UserAgent.IndexOf("Windows NT 6.1") > 0)
                            {
                                OSversion = "Windows 7";
                            }
                            else if (Request.UserAgent.IndexOf("Windows NT 6.2") > 0)
                            {
                                OSversion = "Windows 8";
                            }
                            else if (Request.UserAgent.IndexOf("Windows NT 6.3") > 0)
                            {
                                OSversion = "Windows 8.1";
                            }
                            else if (Request.UserAgent.IndexOf("Mac OS") > 0)
                            {
                                OSversion = "Mac OS";
                            }
                            else if (Request.UserAgent.IndexOf("Linux") > 0)
                            {
                                OSversion = "Linux";
                            }
                            else
                            {
                                OSversion = "Unknown";
                            }
                            if (Request.UserAgent.IndexOf("WOW64") > 0 || Request.UserAgent.IndexOf("Win64") > 0)
                            {
                                Platform = "64 Bit";
                            }
                            else
                                Platform = "32 Bit";
                            #endregion

                            try
                            {
                                //var objUser = db.Users.Where(a => a.UserID == Userid).First();
                                var objUserLog = new UserLogin { UserID = currentUser.UserId, SessionID = Session.SessionID, LoginDate = DateTime.Now, IP = ip, BrowserName = Request.Browser.Browser, BrowserVersion = Request.Browser.Version, OSVersion = OSversion, PlatformType = Platform };
                                if (System.Text.RegularExpressions.Regex.IsMatch(Request.UserAgent, @"Trident/7.0"))
                                {
                                    objUserLog.BrowserName = "IE";
                                    objUserLog.BrowserVersion = "11";
                                }

                                if (System.Text.RegularExpressions.Regex.IsMatch(Request.UserAgent, @"Trident/7.*rv:11"))
                                {
                                    objUserLog.BrowserName = "IE";
                                    objUserLog.BrowserVersion = "11";
                                }
                                //CoupleSessionAndFormsAuth
                                HttpContext.Session["UserName"] = model.UserName;
                                //
                                db.UserLogins.Add(objUserLog);
                                db.SaveChanges();
                            }
                            catch (Exception ex)
                            { }
                            if (Session["init"] != null && Session["init"].ToString() != "" && Convert.ToInt16(Session["init"].ToString()) == 1)
                                return RedirectToAction("Checkout");
                            else
                                return RedirectToLocal(returnUrl);
                        }
                    }

                }
                else
                {
                    ModelState.AddModelError("UserName", @LMSResourse.Admin.Login.msgErrorInvalidLogin);
                }
            }

            // If we got this far, something failed, redisplay form
            return View();
        }

        public string GetUserRolesSession(IList<string> ObjUserRoles)
        {
            string ReturnString = "";
            foreach (var x in ObjUserRoles)
            {
                switch (x)
                {
                    case "Administrator":
                        ReturnString += (ReturnString.Length > 0) ? ",1" : "1";
                        break;
                    case "Group Admin":
                        ReturnString += (ReturnString.Length > 0) ? ",2" : "2";
                        break;
                    case "Learner":
                        ReturnString += (ReturnString.Length > 0) ? ",3" : "3";
                        break;
                    case "Course Admin":
                        ReturnString += (ReturnString.Length > 0) ? ",4" : "4";
                        break;
                    case "Course Creator":
                        ReturnString += (ReturnString.Length > 0) ? ",5" : "5";
                        break;
                    case "Course Reviewer":
                        ReturnString += (ReturnString.Length > 0) ? ",6" : "6";
                        break;
                    case "Course Publisher":
                        ReturnString += (ReturnString.Length > 0) ? ",7" : "7";
                        break;
                    case "Parent":
                        ReturnString += (ReturnString.Length > 0) ? ",8" : "8";
                        break;
                    case "Teacher":
                        ReturnString += (ReturnString.Length > 0) ? ",9" : "9";
                        break;
                }
            }
            return ReturnString;
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            long Userid = 0;
            Userid = Convert.ToInt64(Session["UserID"]);
            if (Userid != 0)
            {
                var objUserLogin = db.UserLogins.Where(u => u.UserID == Userid && u.SessionID == Session.SessionID).OrderByDescending(a => a.LoginDate).FirstOrDefault();
                if (objUserLogin != null)
                {
                    objUserLogin.LogoutDate = DateTime.Now;
                    db.SaveChanges();
                }
            }
            AuthenticationManager.SignOut();

            if (Session["init"] != null && Session["init"].ToString() != "" && Convert.ToInt16(Session["init"].ToString()) == 1)
            {
                Session.Clear();
                return Redirect(ConfigurationManager.AppSettings["CartUrl"]);
            }
            else
            {
                Session.Clear();
                return RedirectToAction("login", "Account");
            }
        }
        // GET: Account/LangSelection  
        [AllowAnonymous]
        [HttpPost]
        public ActionResult LangSelection(string lang)
        {
            try
            {
                if (lang == "1")
                {
                    Session["lang"] = "en-US";
                }
                else if (lang == "2")
                {
                    Session["lang"] = "hi";
                }
                else
                {
                    Session["lang"] = "hi";
                }
                if (Session["lang"] != null)
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(Convert.ToString(Session["lang"]));
                    System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(Convert.ToString(Session["lang"]));
                }

                return Json(new
                {
                    msg = "Successfully added "
                });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        //
        [ChildActionOnly]
        public ActionResult RemoveAccountList()
        {
            var linkedAccounts = UserManager.GetLogins(User.Identity.GetUserId());
            ViewBag.ShowRemoveButton = HasPassword() || linkedAccounts.Count > 1;
            return (ActionResult)PartialView("_RemoveAccountPartial", linkedAccounts);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && UserManager != null)
            {
                UserManager.Dispose();
                UserManager = null;
            }
            base.Dispose(disposing);
        }

        public ActionResult GerUserGroups()
        {
            int languageId = 0;
            long UserId = Convert.ToInt64(Session["UserID"].ToString());
            languageId = int.Parse(Session["LanguageId"].ToString());

            var grpList = db.GetUserGroups(UserId, languageId);
            var y = from x in grpList
                    select new UserGroupsLocal { GroupId = x.GroupID.ToString(), GroupName = x.GroupName };
            return Json(y, JsonRequestBehavior.AllowGet);
        }
        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private async Task SignInAsync(ApplicationUser user, bool isPersistent)
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie);
            var identity = await UserManager.CreateIdentityAsync(user, DefaultAuthenticationTypes.ApplicationCookie);
            AuthenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = isPersistent }, identity);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private bool HasPassword()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            Error
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (String.IsNullOrEmpty(returnUrl))
            {
                return RedirectToAction("Index", "Home");
            }
            else if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            ForgotPasswordViewModel x = new ForgotPasswordViewModel();
            return View(x);

        }

        /*As this method not in use so will remove it later on after development of new user interface*/
        [HttpPost]
        [AllowAnonymous]
        //[ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var objUser = db.UserProfiles.Where(us => us.EmailAddress == model.EmailAddress && us.Id != null && us.IsDelete == false).FirstOrDefault(); //&& us.IsDelete == false
                if (objUser != null)
                {
                    var UserExists = db.AspNetUsers.Where(x => x.UserName == model.EmailAddress).FirstOrDefault(); ;
                    if (UserExists != null)
                    {
                        DateTime dt = System.DateTime.Now.AddDays(1);
                        double span = ConvertDateTimeToTimestamp(dt);
                        string url = System.Configuration.ConfigurationManager.AppSettings["InstanceURL"].ToString() + "/Account/Resetpassword?&code=" + UserExists.Id + "&token=" + span.ToString();
                        LMS.Controllers.UserManagementController Reset = new UserManagementController();
                        //Reset.ResetPasswordandSendMail(11, model.EmailAddress);
                        Reset.SendMailToUser("FPASS", model.EmailAddress, url);
                        Session["ResetID"] = UserExists.Id;
                        Session["ResetExpTime"] = System.DateTime.Now.AddDays(1);
                        return RedirectToAction("Login", "Account");
                    }
                }

            }

            // If we got this far, something failed, redisplay form
            //return View(model);
            return PartialView("_ForgotPassword");
        }

        #region //Check User Email Exists
        [HttpPost]
        [AllowAnonymous]
        public string EmailAddressExists(string forgotemail)
        {
            var emailcount = from up in db.UserProfiles
                             where up.EmailAddress == forgotemail && up.Status == true && up.IsDelete == false
                             select up;
            if (emailcount.Count() > 0)
                return "1";

            return "0";
        }
        #endregion

        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code, string token)
        {
            TempData["msg"] = "";

            string ResetUserID = code != null ? code : "";
            DateTime expTime = ConvertTimestampFromDateTime(Convert.ToDouble(token));
            ResetPasswordViewModel model = new ResetPasswordViewModel();
            if (ResetUserID != null && ResetUserID != "")
            {
                if (expTime >= System.DateTime.Now)
                {
                    AspNetUser ANU = db.AspNetUsers.Where(a => a.Id == ResetUserID).FirstOrDefault();
                    if (ANU != null)
                    {

                        model.Code = ANU.Id;
                        return View(model);
                    }
                }
                TempData["msg"] = "This link has been expired. Please make new request";
                return View(model);
            }
            return RedirectToAction("Login", "Account");
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            TempData["msg"] = "";
            model.Email = "example@gmail.com";
            if (ModelState.IsValid)
            {
                UserManager.RemovePassword(model.Code);
                UserManager.AddPassword(model.Code, model.ConfirmPassword);
                TempData["msg"] = "Your password has been reset successfully.";
            }

            return View(model);
        }

        public static DateTime ConvertTimestampFromDateTime(double timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return origin.AddSeconds(timestamp);
        }

        public static double ConvertDateTimeToTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date.ToLocalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }
        
        #endregion
        
		

        [AllowAnonymous]
        public ActionResult AjaxForgotPassword(ForgotPasswordViewModel model)
        {
            var objUser = db.UserProfiles.Where(us => us.EmailAddress == model.EmailAddress && us.Id != null && us.IsDelete == false).FirstOrDefault(); //&& us.IsDelete == false
            if (objUser != null)
            {
                var UserExists = db.AspNetUsers.Where(x => x.UserName == model.EmailAddress).FirstOrDefault(); ;
                if (UserExists != null)
                {
                    DateTime dt = System.DateTime.Now.AddDays(1);
                    double span = ConvertDateTimeToTimestamp(dt);
                    string url = System.Configuration.ConfigurationManager.AppSettings["InstanceURL"].ToString() + "/Account/Resetpassword?&code=" + UserExists.Id + "&token=" + span.ToString();
                    LMS.Controllers.UserManagementController Reset = new UserManagementController();

                    Reset.SendMailToUser("FPASS", model.EmailAddress, url);
                    Session["ResetID"] = UserExists.Id;
                    Session["ResetExpTime"] = System.DateTime.Now.AddDays(1);

                    return Json(new { aaData = true }, JsonRequestBehavior.AllowGet);
                }
            }

            return Json(new { aaData = false }, JsonRequestBehavior.AllowGet);
        }     

    }
}
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

		
        //
        #region // Create Self Registration User
        [AllowAnonymous]
        public ActionResult CreateSelfRegUser()
        {
            SelfRegistration selfreg = new SelfRegistration();
            return View(selfreg);
        }

        /*As this method not in use so will remove it later on after development of new user interface*/
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<ActionResult> _CreateSelfRegUser(CLSLms.MetaData.SelfRegisterMetaData selfregistration)
        {
            if (ModelState.IsValid)
            {
                var objSelfReg = new SelfRegistration();
                var userdenycount = db.SelfRegistrations.Where(self => self.EmailAddress == selfregistration.Email_Address && self.IsApprove == 2).Count();
                if (userdenycount == 0)
                {
                    objSelfReg = new SelfRegistration();
                    objSelfReg.FirstName = selfregistration.FirstName;
                    objSelfReg.LastName = selfregistration.LastName;
                    objSelfReg.EmailAddress = selfregistration.Email_Address;
                    objSelfReg.IsApprove = 0;
                    objSelfReg.RegistrationDate = DateTime.Now;
                    db.SelfRegistrations.Add(objSelfReg); // add record in SelfRegistration table in data base
                    db.SaveChanges();
                }
                else
                {
                    objSelfReg = db.SelfRegistrations.Where(self => self.EmailAddress == selfregistration.Email_Address && self.IsApprove == 2).FirstOrDefault();
                    if (objSelfReg != null)
                    {
                        objSelfReg.FirstName = selfregistration.FirstName;
                        objSelfReg.LastName = selfregistration.LastName;
                        objSelfReg.IsApprove = 0;
                        objSelfReg.RegistrationDate = DateTime.Now;
                        db.SaveChanges();
                    }
                }

                // Checking any Order exists  in the session 
                if (Session["OrderId"] != null && Session["OrderId"].ToString() != "")
                {
                    //If any order exist then approve user  manually and send login detail mail to user 
                    var selfUser = db.SelfRegistrations.Where(self => self.EmailAddress == selfregistration.Email_Address).FirstOrDefault();
                    var result = await ApproveSelfRegisterdUserManually(selfregistration.Email_Address, (selfUser != null ? Convert.ToInt32(selfUser.SelfRegistrationId) : 0));
                    if (result)
                    {
                        //return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", "Account") });
                        return Json(new { success = true, returnUrl = Url.Action("Checkout", "Account") });
                    }
                }

                #region Sending mail to admin after self registration
                var objInstance = (from o in db.InstanceInfoes
                                   where o.InstanceID == 1
                                   select new { o.InstanceTitle, o.HostEmail, o.URL, o.SmtpIPv4 }).FirstOrDefault();
                if (objInstance != null)
                {
                    var objEmail = (from oEmail in db.Emails
                                    where oEmail.MailCode == "NEWU" && oEmail.IsOn == true
                                    select new { oEmail.Subject, oEmail.Body, oEmail.ID }).FirstOrDefault();
                    if (objEmail != null)
                    {
                        string fromEmail = objInstance.HostEmail;
                        string subject = objEmail.Subject.Replace("{InstanceTitle}", objInstance.InstanceTitle);

                        string body = objEmail.Body;
                        body = body.Replace("{InstanceTitle}", objInstance.InstanceTitle).Replace("{InstanceURL}", objInstance.URL).Replace("{FirstName}", HttpUtility.HtmlEncode(selfregistration.FirstName)).Replace("{LastName}", HttpUtility.HtmlEncode(selfregistration.LastName)).Replace("{Email}", HttpUtility.HtmlEncode(selfregistration.Email_Address)); //edit it
                        body = body.Replace("{approve_url}", (System.Web.Configuration.WebConfigurationManager.AppSettings["InstanceURL"] + "/SelfRegistration/ApproveSelfRegUsers"));
                        try
                        {
                            var sqlquery = "Select Top 1 * from UserProfile" +
                                           " Join AspNetUserRoles on AspNetUserRoles.UserId = UserProfile.Id" +
                                           " where AspNetUserRoles.RoleId =  '19DA82D0-4002-474F-8983-306FBC1C8A9E'" +
                                           " and UserProfile.IsDelete = 0";
                            IEnumerable<UserProfile> Objuser = null;
                            Objuser = db.Database.SqlQuery<UserProfile>(sqlquery).ToList();
                            if (Objuser.Count() > 0)
                            {
                                foreach (var usr in Objuser)
                                {
                                    MailEngine.Send(selfregistration.Email_Address, usr.EmailAddress, subject, body, objInstance.SmtpIPv4);
                                    MailEngine oLog = new MailEngine();

                                    if (userdenycount == 0)
                                    {
                                        var objSelfReg1 = (from s in db.SelfRegistrations
                                                           where s.EmailAddress == objSelfReg.EmailAddress
                                                           select new { s.SelfRegistrationId }).FirstOrDefault();
                                        if (objSelfReg1 != null)
                                            oLog.LogEmail(selfregistration.Email_Address, objEmail.ID, usr.UserId, objSelfReg1.SelfRegistrationId);
                                        //objUser.IsRegisterMailSend = true;
                                        //db.SaveChanges();
                                    }
                                    else
                                    {
                                        oLog.LogEmail(selfregistration.Email_Address, objEmail.ID, usr.UserId, objSelfReg.SelfRegistrationId);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
                #endregion
                // return RedirectToAction("Login", "Account");
                return Json(new { success = true, returnUrl = "" });
            }
            return Json(new { success = false, error = ModelState });
        }
        [HttpGet]
        [AllowAnonymous]
        public string UniqueSelfRegEmailAddress(string email)
        {
            var emailaddcount = from selfreg in db.SelfRegistrations
                                where selfreg.EmailAddress == email && selfreg.IsApprove != 2
                                select selfreg;
            if (emailaddcount.Count() > 0)
                return "1";
            else
            {
                var useremailcount = from user in db.UserProfiles
                                     where user.EmailAddress == email
                                     select user;
                if (useremailcount.Count() > 0)
                    return "1";
            }

            return "";
        }
		
		public async Task<Boolean> ApproveSelfRegisterdUserManually(string email, int uid, SelfRegistrationModel selfregistration = null)
        {
            SelfRegistrationController objSelfRegistrationController = new SelfRegistrationController();
            var errorMessage = "";

            var sid = Convert.ToInt32(uid);
            var selfreg = db.SelfRegistrations.Where(x => x.SelfRegistrationId == sid).FirstOrDefault();
            var DefaultOrgId = 0;
            if (selfreg != null)
            {
                var user = new ApplicationUser() { UserName = selfreg.EmailAddress };
                var pwd = System.Configuration.ConfigurationManager.AppSettings["defaultPassword"].ToString();//objSelfRegistrationController.AjaxGeneratePassword();
                var result = UserManager.Create(user, pwd.ToString()); // create a user by usermanager by passing username(i.e email address) and password. this will create record in aspNetUsers table.

                if (result.Succeeded) // check the status of user creation.
                {
                    #region // create the object of user profile in which other information of user is saved
                    var objUser = new UserProfile();
                    objUser.Id = user.Id;
                    objUser.EmployeeID = "";
                    objUser.FirstName = selfreg.FirstName;
                    objUser.LastName = selfreg.LastName;
                    objUser.EmailAddress = selfreg.EmailAddress;
                    objUser.ContactNo = "";
                    objUser.ManagerName = "";
                    objUser.Status = true;
                    objUser.RegistrationDate = DateTime.Now;
                    objUser.DateLastModified = DateTime.Now;
                    objUser.LastModifiedByID = Convert.ToInt64(Session["UserID"]);
                    objUser.LanguageId = db.InstanceInfoes.Find(1).DefaultLanguage;
                    objUser.SchoolId = selfregistration.SchoolID;
                    objUser.ContactNo = selfregistration.ContactNo;

                    if (ConfigurationManager.AppSettings["DefaultOrganization"] != null && ConfigurationManager.AppSettings["DefaultOrganization"].ToString() != "" && Convert.ToInt32(ConfigurationManager.AppSettings["DefaultOrganization"].ToString()) != 0)
                    {
                        DefaultOrgId = Convert.ToInt32(ConfigurationManager.AppSettings["DefaultOrganization"].ToString());
                        objUser.OrganisationID = DefaultOrgId;
                    }

                    #region //check the Optional data depending on selected organisation.
                    var objOrgSettings = db.UserProfileSettingsOrgs.Where(x => x.OrganisationID == DefaultOrgId).Select(x => x).ToList();
                    if (objOrgSettings.Count > 0)
                    {
                        var objorgsetprofile1 = objOrgSettings.Where(x => x.ProfileSettingID == 1 && x.OrganisationID == DefaultOrgId).ToList();
                        if (objorgsetprofile1.Count > 0)
                        {
                            if (objOrgSettings.Where(x => x.ProfileSettingID == 1).SingleOrDefault().ProfileType == 2)
                            {

                                objUser.Option1 = "Please enter here your Profile Values Title";
                            }
                            else
                                objUser.Option1 = null;
                        }
                        else
                            objUser.Option1 = null;

                        var objorgsetprofile2 = objOrgSettings.Where(x => x.ProfileSettingID == 2 && x.OrganisationID == DefaultOrgId).ToList();
                        if (objorgsetprofile2.Count > 0)
                        {
                            if (objOrgSettings.Where(x => x.ProfileSettingID == 2).SingleOrDefault().ProfileType == 2)
                            {
                                objUser.Option2 = "Please enter here your Profile Values Title";
                            }
                            else
                                objUser.Option2 = null;
                        }
                        else
                            objUser.Option2 = null;
                    }
                    #endregion

                    objUser.IsDelete = false;
                    db.UserProfiles.Add(objUser); // add record in userprofile table in data base
                    db.SaveChanges(); // user creation is completed.
                    if (selfregistration != null && !string.IsNullOrEmpty(selfregistration.UserIDs))
                    {
                        string[] studentIds = selfregistration.UserIDs.Split(',');
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

                    #region // default role. Add the default role to user i.e learner.
                    var res = UserManager.AddToRole(user.Id.ToString(), db.InstanceInfoes.Find(1).RoleName);
                    db.SaveChanges();
                    #endregion

                    #region // Assigning groups to user
                    foreach (var y in db.Groups.Where(a => a.GroupID == selfregistration.GroupID || selfregistration.GroupIDs.Contains(a.GroupID.ToString())).OrderByDescending(b => b.CreationDate).ToList())
                    {
                        UserGroup ObjUs = new UserGroup(); // create object of usergroup in which user and group relationship is saved.
                        ObjUs.UserId = objUser.UserId;
                        ObjUs.GroupID = y.GroupID;
                        ObjUs.LastModifiedByID = objUser.UserId;
                        ObjUs.DateLastModified = DateTime.Now;
                        db.UserGroups.Add(ObjUs);
                        db.SaveChanges();
                    }
                    #endregion

                    #region //Update Self Registration with userid and approve status
                    selfreg.UserId = objUser.UserId;
                    selfreg.IsApprove = 1;
                    db.SaveChanges();
                    #endregion

                    // email send to user with username and password information.
                    objSelfRegistrationController.SendApprovalMailToUser(selfreg.SelfRegistrationId, selfreg.EmailAddress, pwd);

                    #region validate and Sign In user
                    var userProfile = db.UserProfiles.Where(x => x.EmailAddress == selfreg.EmailAddress && x.IsDelete == false && x.Status == true).FirstOrDefault();
                    var userDetail = await UserManager.FindAsync(selfreg.EmailAddress, pwd);
                    if (userDetail != null && userProfile != null && selfregistration == null)
                    {
                        {
                            await SignInAsync(userDetail, false);
                            List<MAclActions> oUserInRoles = db.Database.SqlQuery<MAclActions>("SELECT AM.ACLActionID,ActionFQN,UR.RoleId FROM ACLMatrix AS AM INNER JOIN AspNetUserRoles AS UR ON AM.RoleID=UR.RoleId INNER JOIN ACLActions AS ACLS ON ACLS.ACLActionID=AM.ACLActionID WHERE AM.IsAccess = 1 and UR.UserId IN(SELECT AspNetUsers.Id FROM AspNetUsers WHERE Username=@Username)", new SqlParameter("Username", userDetail.UserName)).ToList();
                            //var oUserInRoles = db.Database.SqlQuery<MAclActions>("SELECT AM.ACLActionID,ActionFQN,UR.RoleId FROM ACLMatrix AS AM INNER JOIN AspNetUserRoles AS UR ON AM.RoleID=UR.RoleId INNER JOIN ACLActions AS ACLS ON ACLS.ACLActionID=AM.ACLActionID WHERE AM.IsAccess = 1 and UR.UserId IN(SELECT UserId FROM AspNetUsers WHERE Username=@Username)", new SqlParameter("Username", model.UserName)).ToList();

                            Session["MAclActions"] = oUserInRoles;

                            var currentUser = userProfile;
                            Session["UserID"] = currentUser.UserId;
                            Session["LastName"] = currentUser.LastName; //session used for scorm course API12.vb
                            Session["Firstname"] = currentUser.FirstName; //session used for scorm course API12.vb
                            Session["LanguageId"] = currentUser.LanguageId;
                            Session["IsGroupAdmin"] = UserManager.IsInRole(currentUser.Id, ConfigurationManager.AppSettings["GroupAdminRole"].ToString());
                            Session["UserRoles"] = GetUserRolesSession(UserManager.GetRoles(currentUser.Id));
                            Session["IsAdminView"] = ((Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(2) || (Session["UserRoles"].ToString()).Split(',').Select(int.Parse).ToArray().Contains(1));

                            Session["IsSuperAdmin"] = false;
                            Session["IsSuperAdmin"] = false;
                            Session["CourseMangerRole"] = "NA";
                            return true;
                        }
                    }
                    else if (userDetail != null && userProfile != null && selfregistration != null)
                    {
                        return true;
                    }
                    else // if any error exist at the time of user creation add all the error's in model
                    {
                        foreach (var x in result.Errors)
                            errorMessage += x.ToString();
                        ModelState.AddModelError("OrganisationID", errorMessage);
                    }
                    #endregion
                }
            }

            return false;
        }
		
		[AllowAnonymous]
        public async Task<ActionResult> AjaxCreateSelfRegUser(SelfRegistrationModel selfregistration)
        {
            bool autoapproveSelfRegistration = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["AutoapproveSelfRegistration"]);
            //if (ModelState.IsValid)
            //{
            var objSelfReg = new SelfRegistration();
            var userdenycount = db.SelfRegistrations.Where(self => self.EmailAddress == selfregistration.EmailAddress && self.IsApprove == 2).Count();
            if (userdenycount == 0)
            {
                objSelfReg = new SelfRegistration();
                objSelfReg.FirstName = selfregistration.FirstName;
                objSelfReg.LastName = selfregistration.LastName;
                objSelfReg.EmailAddress = selfregistration.EmailAddress;
                objSelfReg.IsApprove = 0;
                objSelfReg.RegistrationDate = DateTime.Now;
                objSelfReg.SchoolId = selfregistration.SchoolID;
                objSelfReg.RoleName = (selfregistration.RoleId == 1 ? "Learner" : selfregistration.RoleId == 2 ? "Parent" : "Teacher");
                objSelfReg.GroupIDs = selfregistration.GroupIDs;
                objSelfReg.UserIDs = selfregistration.UserIDs;
                objSelfReg.ContactNo = selfregistration.ContactNo;
                db.SelfRegistrations.Add(objSelfReg); // add record in SelfRegistration table in data base
                db.SaveChanges();
                if (autoapproveSelfRegistration && selfregistration.RoleId == 1)
                {
                    bool isApproved = await ApproveSelfRegisterdUserManually(objSelfReg.EmailAddress, Convert.ToInt32(objSelfReg.SelfRegistrationId), selfregistration);
                    if (isApproved)
                    {
                        string userRegisterMessage = LMSResourse.Admin.Login.msgConfirmSelfRegistration + "<br/>" +
                        "<b>" + LMSResourse.User.Login.login.lblUserName + ":</b> " + objSelfReg.EmailAddress + " and <br/><b>" + LMSResourse.User.Login.login.lblPassword + ":</b> " + System.Configuration.ConfigurationManager.AppSettings["defaultPassword"].ToString();
                        return Json(new { aaData = userRegisterMessage }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            else
            {
                objSelfReg = db.SelfRegistrations.Where(self => self.EmailAddress == selfregistration.EmailAddress && self.IsApprove == 2).FirstOrDefault();
                if (objSelfReg != null)
                {
                    objSelfReg.FirstName = selfregistration.FirstName;
                    objSelfReg.LastName = selfregistration.LastName;
                    objSelfReg.IsApprove = 0;
                    objSelfReg.RegistrationDate = DateTime.Now;
                    db.SaveChanges();
                }
            }

            if (Session["OrderId"] != null && Session["OrderId"].ToString() != "" && autoapproveSelfRegistration == false)
            {
                var selfUser = db.SelfRegistrations.Where(self => self.EmailAddress == selfregistration.EmailAddress).FirstOrDefault();
                bool result = await ApproveSelfRegisterdUserManually(selfregistration.EmailAddress, (selfUser != null ? Convert.ToInt32(selfUser.SelfRegistrationId) : 0));
                if (result)
                {
                    //return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", "Account") });
                    return Json(new { aaData = result, returnUrl = "Checkout" }, JsonRequestBehavior.AllowGet);
                }
            }
            //Sending mail to admin after self registration
            if (!autoapproveSelfRegistration || selfregistration.RoleId > 1)
            {
                var objInstance = (from o in db.InstanceInfoes
                                   where o.InstanceID == 1
                                   select new { o.InstanceTitle, o.HostEmail, o.URL, o.SmtpIPv4 }).FirstOrDefault();
                if (objInstance != null)
                {
                    var objEmail = (from oEmail in db.Emails
                                    where oEmail.MailCode == "NEWU" && oEmail.IsOn == true
                                    select new { oEmail.Subject, oEmail.Body, oEmail.ID }).FirstOrDefault();
                    if (objEmail != null)
                    {
                        string fromEmail = objInstance.HostEmail;
                        string subject = objEmail.Subject.Replace("{InstanceTitle}", objInstance.InstanceTitle);

                        string body = objEmail.Body;
                        body = body.Replace("{InstanceTitle}", objInstance.InstanceTitle).Replace("{InstanceURL}", objInstance.URL).Replace("{FirstName}", HttpUtility.HtmlEncode(selfregistration.FirstName)).Replace("{LastName}", HttpUtility.HtmlEncode(selfregistration.LastName)).Replace("{Email}", HttpUtility.HtmlEncode(selfregistration.EmailAddress)); //edit it
                        body = body.Replace("{approve_url}", (System.Web.Configuration.WebConfigurationManager.AppSettings["InstanceURL"] + "/SelfRegistration/ApproveSelfRegUsers"));
                        try
                        {
                            var sqlquery = "Select Top 1 * from UserProfile" +
                                           " Join AspNetUserRoles on AspNetUserRoles.UserId = UserProfile.Id" +
                                           " where AspNetUserRoles.RoleId =  '19DA82D0-4002-474F-8983-306FBC1C8A9E'" +
                                           " and UserProfile.IsDelete = 0";
                            IEnumerable<UserProfile> Objuser = null;
                            Objuser = db.Database.SqlQuery<UserProfile>(sqlquery).ToList();
                            if (Objuser.Count() > 0)
                            {
                                foreach (var usr in Objuser)
                                {
                                    MailEngine.Send(selfregistration.EmailAddress, usr.EmailAddress, subject, body, objInstance.SmtpIPv4);
                                    MailEngine oLog = new MailEngine();

                                    if (userdenycount == 0)
                                    {
                                        var objSelfReg1 = (from s in db.SelfRegistrations
                                                           where s.EmailAddress == objSelfReg.EmailAddress
                                                           select new { s.SelfRegistrationId }).FirstOrDefault();
                                        if (objSelfReg1 != null)
                                            oLog.LogEmail(selfregistration.EmailAddress, objEmail.ID, usr.UserId, objSelfReg1.SelfRegistrationId);
                                        //objUser.IsRegisterMailSend = true;
                                        //db.SaveChanges();
                                    }
                                    else
                                    {
                                        oLog.LogEmail(selfregistration.EmailAddress, objEmail.ID, usr.UserId, objSelfReg.SelfRegistrationId);
                                    }
                                }
                            }

                            return Json(new { aaData = LMSResourse.Admin.ProfileSettings.msgSelfRegSubmit }, JsonRequestBehavior.AllowGet);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
            }

            return Json(new { aaData = false }, JsonRequestBehavior.AllowGet);
        }

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

        [AllowAnonymous]
        public ActionResult AjaxGetGroupUsers(string groupIDs, int schoolId)
        {
            var users = db.UserProfiles
                .Join(db.UserGroups, up => up.UserId, ug => ug.UserId, (up, ug) => new { UP = up, UG = ug })
                .Join(db.AspNetUserRoles, up => up.UP.Id, ar => ar.UserId, (up, r) => new { AR = r, upr = up })
                .Join(db.AspNetRoles, asur => asur.AR.RoleId, asr => asr.Id, (asur, asr) => new { ASR = asr, ASUR = asur })
                .Where(upf => (upf.ASR.Name.Contains("Learner") &&
                !upf.ASR.Name.Contains("Administrator") &&
                !upf.ASR.Name.Contains("Group Admin") &&
                !upf.ASR.Name.Contains("Course Publisher") &&
                !upf.ASR.Name.Contains("Course Reviewer") &&
                !upf.ASR.Name.Contains("Course Creator")
                ) &&
                groupIDs.Contains(upf.ASUR.upr.UG.GroupID.ToString()) &&
                upf.ASUR.upr.UP.SchoolId == schoolId).Select(x => new { UserId = x.ASUR.upr.UP.UserId, FirstName = x.ASUR.upr.UP.FirstName + " " + x.ASUR.upr.UP.LastName + " - " + x.ASUR.upr.UP.EmailAddress });

            return Json(new { aaData = users }, JsonRequestBehavior.AllowGet);
        }

        [AllowAnonymous]
        public ActionResult AjaxGetBlockOfDistrictByID(int districtId)
        {
            var block = from b in db.DistrictBlocks
                        where b.DistrictId == districtId
                        select new { b.BlockId, b.BlockName };
            return Json(new { data = block }, JsonRequestBehavior.AllowGet);
        }

        [AllowAnonymous]
        public ActionResult AjaxGetSchoolOfBlockByID(int blockId)
        {
            var school = from s in db.DistrictSchools
                         where s.BlockId == blockId
                         select new { s.SchoolId, s.SchoolName };

            return Json(new { data = school }, JsonRequestBehavior.AllowGet);
        }

        [AllowAnonymous]
        public ActionResult SelfRegistrationUser(string returnUrl)
        {
            if (Session["UserID"] != null)
            {
                return RedirectToLocal(returnUrl);
            }
            var DefaultOrgId = 0;
            if (ConfigurationManager.AppSettings["DefaultOrganization"] != null && ConfigurationManager.AppSettings["DefaultOrganization"].ToString() != "" && Convert.ToInt32(ConfigurationManager.AppSettings["DefaultOrganization"].ToString()) != 0)
            {
                DefaultOrgId = Convert.ToInt32(ConfigurationManager.AppSettings["DefaultOrganization"].ToString());
            }
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.GroupList = new SelectList(db.Groups.Where(a => a.GroupID != 1 && a.OrganisationID == DefaultOrgId).OrderBy(g => g.GroupName).Select(g => g), "GroupID", "GroupName");
            ViewBag.DistrictList = new SelectList(db.Districts.OrderBy(d => d.DistrictName).Select(d => d), "DistrictId", "DistrictName");
            ViewBag.BlockList = new SelectList(new List<DistrictBlock>(), "BlockId", "BlockName");
            ViewBag.SchoolList = new SelectList(new List<DistrictSchool>(), "SchoolId", "SchoolName");

            SelfRegistrationModel selfRegistrationModel = new SelfRegistrationModel();
            var UserGroup = new UserGroupsLocalView();

            // Group that is not associeated to any organisation will not be listed in group list.
            UserGroup.AvailableGroups = db.Groups.Where(g => g.IsDeleted == false && g.OrganisationID == DefaultOrgId && g.Status == true && g.GroupID != 1).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString(), GroupName = g.GroupName, IsSelected = false }).OrderBy(g => g.GroupName).ToList();
            UserGroup.SelectedGroups = db.Groups.Where(g => g.GroupID == 0).Select(g => new UserGroupsLocal { GroupId = g.GroupID.ToString(), GroupName = g.GroupName, IsSelected = true }).OrderBy(g => g.GroupName).ToList();
            selfRegistrationModel.UserGroupList = UserGroup;
            //LoginViewModel

            return View(selfRegistrationModel);
        }
        #endregion
    }
}
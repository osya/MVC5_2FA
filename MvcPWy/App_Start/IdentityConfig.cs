﻿using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using MvcPWy.Models;
using SendGrid;
using System.Net;
using System.Configuration;
using System.Diagnostics;
using System.Net.Mail;
using Twilio;

namespace MvcPWy
{
   public class EmailService : IIdentityMessageService
   {
      public async Task SendAsync(IdentityMessage message)
      {
         await ConfigSendGridasync(message);
      }

      // Use NuGet to install SendGrid (Basic C# client lib) 
      private static async Task ConfigSendGridasync(IdentityMessage message)
      {
        var myMessage = new SendGridMessage
        {
            From = new MailAddress("info@vosipov.com", "Valeriy O."),
            Subject = message.Subject,
            Text = message.Body,
            Html = message.Body
        };
        myMessage.AddTo(message.Destination);
         
        var credentials = new NetworkCredential(
            ConfigurationManager.AppSettings["mailAccount"],
            ConfigurationManager.AppSettings["mailPassword"]
        );

        // Create a Web transport for sending email.
        var transportWeb = new Web(credentials);

        try
         {
            // Send the email.
            if (transportWeb != null)
            {
               await transportWeb.DeliverAsync(myMessage);
            }
            else
            {
               Trace.TraceError("Failed to create Web transport.");
               await Task.FromResult(0);
            }
         }
         catch (Exception ex)
         {
            Trace.TraceError(ex.Message + " SendGrid probably not configured correctly.");
         }
      }
   }

   public class SmsService : IIdentityMessageService
   {
      public Task SendAsync(IdentityMessage message)
      {
         var twilio = new TwilioRestClient(
            ConfigurationManager.AppSettings["TwilioSid"],
            ConfigurationManager.AppSettings["TwilioToken"]
        );
         var result = twilio.SendMessage(
             ConfigurationManager.AppSettings["TwilioFromPhone"],
            message.Destination, message.Body);

         // Status is one of Queued, Sending, Sent, Failed or null if the number is not valid
         Trace.TraceInformation(result.Status);

         // Twilio doesn't currently have an async API, so return success.
         return Task.FromResult(0);
      }
   }

   // Configure the application user manager used in this application. UserManager is defined in ASP.NET Identity and is used by the application.
   public class ApplicationUserManager : UserManager<ApplicationUser>
   {
      public ApplicationUserManager(IUserStore<ApplicationUser> store)
         : base(store)
      {
      }

      public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
      {
         var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));
         // Configure validation logic for usernames
         manager.UserValidator = new UserValidator<ApplicationUser>(manager)
         {
            AllowOnlyAlphanumericUserNames = false,
            RequireUniqueEmail = true
         };

         // Configure validation logic for passwords
         //manager.PasswordValidator = new PasswordValidator
         //{
         //    RequiredLength = 6,
         //    RequireNonLetterOrDigit = true,
         //    RequireDigit = true,
         //    RequireLowercase = true,
         //    RequireUppercase = true,
         //};

         // Configure user lockout defaults
         manager.UserLockoutEnabledByDefault = true;
         manager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(5);
         manager.MaxFailedAccessAttemptsBeforeLockout = 5;

         // Register two factor authentication providers. This application uses Phone and Emails as a step of receiving a code for verifying the user
         // You can write your own provider and plug it in here.
         manager.RegisterTwoFactorProvider("Phone Code", new PhoneNumberTokenProvider<ApplicationUser>
         {
            MessageFormat = "Your security code is {0}"
         });
         manager.RegisterTwoFactorProvider("Email Code", new EmailTokenProvider<ApplicationUser>
         {
            Subject = "Security Code",
            BodyFormat = "Your security code is {0}"
         });
         manager.EmailService = new EmailService();
//         manager.SmsService = new SmsService();
         var dataProtectionProvider = options.DataProtectionProvider;
         if (dataProtectionProvider != null)
         {
            manager.UserTokenProvider =
                new DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider.Create("ASP.NET Identity"));
         }
         return manager;
      }
   }

   // Configure the application sign-in manager which is used in this application.
   public class ApplicationSignInManager : SignInManager<ApplicationUser, string>
   {
      public ApplicationSignInManager(ApplicationUserManager userManager, IAuthenticationManager authenticationManager)
         : base(userManager, authenticationManager)
      {
      }

      public override Task<ClaimsIdentity> CreateUserIdentityAsync(ApplicationUser user)
      {
         return user.GenerateUserIdentityAsync((ApplicationUserManager)UserManager);
      }

      public static ApplicationSignInManager Create(IdentityFactoryOptions<ApplicationSignInManager> options, IOwinContext context)
      {
         return new ApplicationSignInManager(context.GetUserManager<ApplicationUserManager>(), context.Authentication);
      }
   }
}

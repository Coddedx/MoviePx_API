using System.Text;
using api.Data;
using api.Interfaces;
using api.Models;
using api.Repository;
using api.Service;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "MoviePx Demo", Version = "v1" });
    
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header, //Bearer token’ın HTTP başlığında bulunacağı
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http, //Güvenlik şeması türünü belirtir. Burada Http kullanılıyor çünkü JWT token HTTP başlığında iletiliyor
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });   
    
    // OAuth2 AuthorizationCode akışı ekleme
    // option.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    // {
    //     Type = SecuritySchemeType.OAuth2,
    //     Flows = new OpenApiOAuthFlows // oauth un farklı akışları var (burda AuthorizationCode akışı kullanılıyor)
    //     {
    //         AuthorizationCode = new OpenApiOAuthFlow
    //         {
    //             AuthorizationUrl = new Uri("https://accounts.google.com/o/oauth2/v2/auth"),
    //             TokenUrl = new Uri("https://oauth2.googleapis.com/token"),
    //             Scopes = new Dictionary<string, string>
    //             {
    //                 { "email", "Access to your email" },
    //                 { "profile", "Access to your profile" }
    //             }
    //         }
    //     }
    // });

    // Güvenlik gereksinimleri OAUTH İÇİN olan sonradan SONRADAN!!!!!!!!!!!!!
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[]{}
        }//,
        // {
        //     new OpenApiSecurityScheme
        //     {
        //         Reference = new OpenApiReference
        //         {
        //             Type = ReferenceType.SecurityScheme,
        //             Id = "oauth2"
        //         }
        //     },
        //     new string[]{}
        // }
    });
});

builder.Services.AddDbContext<ApplicationDBContext>(options => {
   options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
   options.Password.RequireDigit = true;
   options.Password.RequireLowercase = true;
   options.Password.RequireUppercase = true;
   options.Password.RequireNonAlphanumeric = true;
 options.Password.RequiredLength = 12;
}).AddEntityFrameworkStores<ApplicationDBContext>();


builder.Services.AddAuthentication(options => {
   // options.DefaultAuthenticateScheme =  //kimlik doğrulama işlemi sırasında hangi şemanın kullanılacağını
   // options.DefaultChallengeScheme =    //kullanıcı doğrulama gereksinimi olduğunda (kullanıcı kimliği doğrulanmamışsa) hangi kimlik doğrulama şemasının kullanılacağı
   // options.DefaultForbidScheme =      //kullanıcının belirli bir kaynağa erişim izni olmadığı durumda hangi şemanın devreye gireceğini
   // options.DefaultScheme =           //genel olarak hangi kimlik doğrulama şemasının kullanılacağına
   // options.DefaultSignInScheme =    //kullanıcının giriş yaptıktan sonra hangi kimlik doğrulama şemasının kullanılacağını(genellikle cookie authentication kullanıldığında)
   options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; //hem Google OAuth hem de JWT gibi birden fazla kimlik doğrulama yöntemi kullanıyorsanız, bu durumda şemaları doğru bir şekilde yönlendirebilmek için DefaultAuthenticateScheme ve DefaultChallengeScheme kullanmak önemlidir
   options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
}).AddJwtBearer(options => {
   options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"], // token'ın kim tarafından verildiği
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JWT:Audience"], //Token'ın hangi hedefe verildiği kontrol edilir.
        ValidateIssuerSigningKey = true, //Token'ın imzası doğrulanır. Bu, token'ın değiştirilmediğini ve geçerli bir anahtar ile imzalandığını kontrol eder
        IssuerSigningKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JWT:SigningKey"])), //Token'ı imzalayan anahtar.
        ValidateLifetime = true
    };
   
   options.Events = new JwtBearerEvents
   {
      OnAuthenticationFailed = context =>
      {
         Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
         return Task.CompletedTask;
      }//,
    //   OnTokenValidated = context =>
    //   {
    //      Console.WriteLine("JWT Token successfully validated.");
    //      return Task.CompletedTask;
    //   }
    };    
})//;
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.ClientId = builder.Configuration["GoogleOAuth:ClientId"];
    options.ClientSecret = builder.Configuration["GoogleOAuth:ClientSecret"];
    options.CallbackPath = "/google-callback"; // Google geri dönüş URL'si
    
    //options.SaveTokens = true; // Token'ları saklayarak erişim kolaylığı sağlar. 
    //JWT kullanıyorsanız, SaveTokens ile token'lar saklanmaz çünkü JWT, stateless bir yapıdır ve genellikle token'lar istemci tarafında saklanır.
    //Google OAuth2 işlemi sonucunda elde edilen token'lar(accessToken  ve refreshToken), sunucuda saklanmaz (kullanıcının oturum bilgisi (claims) ile birlikte saklar)
    //authentication cookie veya session state içerisinde saklanır 
    //(HttpContext ile ulaşım örneği-> var accessToken = await HttpContext.GetTokenAsync("access_token");)

    // Google'a yönlendirme yapılmadan önce state veya başka bir işlem
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        Console.WriteLine($"Redirecting to: {context.RedirectUri}");
        return Task.CompletedTask;
    };
    options.Events.OnCreatingTicket = context =>
    {
        Console.WriteLine("Google ticket created.");
        return Task.CompletedTask;
    };
});

//DI ile SymmetricSecurityKey sağlıyoruz (yalnızca bir kez oluşturulur ve uygulama boyunca tekrar kullanılabilir)
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:SigningKey"]));
builder.Services.AddSingleton(signingKey);

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOMDbService, OMDbService>();
builder.Services.AddHttpClient<IOMDbService, OMDbService>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>(); //AddSingleton
builder.Services.AddScoped<IUserRatingRepository, UserRatingRepository>(); 

builder.Services.AddScoped<IOAuthService, OAuthService>(); 
builder.Services.AddHttpClient<IOAuthService, OAuthService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
   app.UseSwagger();
   app.UseSwaggerUI(options => 
   {
      options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
      options.RoutePrefix = string.Empty;
   });
}

app.UseHttpsRedirection();

app.UseRouting(); //GOOGLE OAUTH İÇİN SONRADAN!!!!!!!!!!!!!!!!!!!!

app.UseCors(x => x
        .AllowAnyOrigin()
        //.WithOrigins("http://localhost:5188")
        .AllowAnyMethod()
        .AllowAnyHeader()
        //.AllowCredentials() // Eğer frontend ve backend farklı domainlerde çalışıyorsa `AllowCredentials` ile birlikte `AllowAnyOrigin` kullanılamaz.
       //  .SetIsOriginAllowed(origin => true)
);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


using GPMS.Data;
using GPMS.Models;
using GPMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GPMS.Controllers
{
    public class MessageController : Controller
    {
        private readonly AppDbContext _context;

        public MessageController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================
        // INBOX
        // =========================================

        public async Task<IActionResult> Index()
        {
            int currentUserId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)
            );

            // =====================================
            // CHAT LIST
            // =====================================

            var chatList = await _context.MessageReceivers

                .Include(x => x.Message)
                .ThenInclude(x => x.Sender)

                .Where(x => x.ReceiverId == currentUserId)

                .OrderByDescending(x => x.Message.SentAt)

                .Select(x => new
                {
                    EmployeeId =
                        x.Message.Sender.EmployeeId,

                    EmployeeName =
                        x.Message.Sender.EmployeeName,

                    LastMessage =
                        x.Message.Body,

                    Time =
                        x.Message.SentAt
                            .ToString("dd MMM")
                })

                .Distinct()

                .ToListAsync();

            ViewBag.ChatList = chatList;

            // =====================================
            // DEFAULT CHAT
            // =====================================

            var firstChat =
                chatList.FirstOrDefault();

            if (firstChat != null)
            {
                var messages =
                    await _context.Messages

                    .Where(m =>
                        m.SenderId == firstChat.EmployeeId
                        ||
                        m.SenderId == currentUserId
                    )

                    .OrderBy(m => m.SentAt)

                    .Select(m => new
                    {
                        Body = m.Body,

                        IsMine =
                            m.SenderId == currentUserId
                    })

                    .ToListAsync();

                ViewBag.Messages = messages;
            }

            return View();
        }

        // =========================================
        // SENT
        // =========================================

        public async Task<IActionResult> Sent()
        {
            int currentUserId = int.Parse(
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                )
            );

            var sent = await _context.Messages

                .Include(x => x.Receivers)
                .ThenInclude(x => x.Receiver)

                .Where(x =>
                    x.SenderId == currentUserId
                )

                .OrderByDescending(x => x.SentAt)

                .ToListAsync();

            return View(sent);
        }

        // =========================================
        // COMPOSE GET
        // =========================================

        [HttpGet]
        public IActionResult Compose()
        {
            return View();
        }

        // =========================================
        // SEND MESSAGE
        // =========================================

        [HttpPost]
        public async Task<IActionResult> Compose(
            [FromBody] ComposeMessageViewModel vm
        )
        {
            int currentUserId = int.Parse(
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                )
            );

            // =====================================
            // CREATE MESSAGE
            // =====================================

            Message message = new Message
            {
                SenderId = currentUserId,

                Subject = vm.Subject,

                Body = vm.Body,

                SentAt = DateTime.Now
            };

            _context.Messages.Add(message);

            await _context.SaveChangesAsync();

            // =====================================
            // SAVE RECEIVERS
            // =====================================

            foreach (var receiverId in vm.ReceiverIds)
            {
                MessageReceiver receiver =
                    new MessageReceiver
                    {
                        MessageId = message.MessageId,

                        ReceiverId = receiverId,

                        IsRead = false
                    };

                _context.MessageReceivers.Add(receiver);
            }

            await _context.SaveChangesAsync();

            // =====================================
            // RETURN SUCCESS
            // =====================================

            return Json(new
            {
                success = true,

                receiverId = vm.ReceiverIds.FirstOrDefault()
            });
        }

        // =========================================
        // MESSAGE DETAILS
        // =========================================

        public async Task<IActionResult> Details(
            int id
        )
        {
            int currentUserId = int.Parse(
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                )
            );

            var messageReceiver =
                await _context.MessageReceivers

                .Include(x => x.Message)
                .ThenInclude(x => x.Sender)

                .FirstOrDefaultAsync(x =>
                    x.MessageId == id
                    &&
                    x.ReceiverId == currentUserId
                );

            if (messageReceiver == null)
            {
                return NotFound();
            }

            messageReceiver.IsRead = true;

            await _context.SaveChangesAsync();

            return View(messageReceiver);
        }

        // =========================================
        // LIVE EMPLOYEE SEARCH
        // =========================================

        [HttpGet]
        public async Task<IActionResult>
            SearchEmployees(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new List<object>());
            }

            var employees =
                await _context.Employees

                .Where(x =>
                    x.EmployeeName.Contains(term)
                )

                .Select(x => new
                {
                    id = x.EmployeeId,

                    name = x.EmployeeName
                })

                .Take(10)

                .ToListAsync();

            return Json(employees);
        }

        // =========================================
        // LOAD CONVERSATION
        // =========================================

        [HttpGet]
        public async Task<IActionResult>
            GetConversation(int employeeId)
        {
            int currentUserId = int.Parse(
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                )
            );

            var messages =
                await _context.Messages

                .Where(m =>

                    (
                        m.SenderId == currentUserId
                        &&
                        m.Receivers.Any(r =>
                            r.ReceiverId == employeeId
                        )
                    )

                    ||

                    (

                        m.SenderId == employeeId
                        &&
                        m.Receivers.Any(r =>
                            r.ReceiverId == currentUserId
                        )
                    )
                )

                .OrderBy(m => m.SentAt)

                .Select(m => new
                {
                    body = m.Body,

                    isMine =
                        m.SenderId == currentUserId,

                    time =
                        m.SentAt
                })

                .ToListAsync();

            return Json(messages);
        }

        // =========================================
        // QUICK SEND MESSAGE
        // =========================================

        [HttpPost]
        public async Task<IActionResult>
            SendQuickMessage(
                [FromBody] QuickMessageViewModel vm
            )
        {
            int currentUserId = int.Parse(
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                )
            );

            Message message = new Message
            {
                SenderId = currentUserId,

                Subject = "Chat Message",

                Body = vm.Body,

                SentAt = DateTime.Now
            };

            _context.Messages.Add(message);

            await _context.SaveChangesAsync();

            MessageReceiver receiver =
                new MessageReceiver
                {
                    MessageId = message.MessageId,

                    ReceiverId = vm.ReceiverId,

                    IsRead = false
                };

            _context.MessageReceivers.Add(receiver);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetChatList()
        {
            int currentUserId = int.Parse(
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                )
            );

            var receivedChats = await _context.MessageReceivers

                .Include(x => x.Message)
                .ThenInclude(x => x.Sender)

                .Where(x =>
                    x.ReceiverId == currentUserId
                )

                .Select(x => new
                {
                    employeeId =
                        x.Message.Sender.EmployeeId,

                    employeeName =
                        x.Message.Sender.EmployeeName,

                    lastMessage =
                        x.Message.Body,

                    sentAt =
                        x.Message.SentAt,

                    unreadCount =
                        _context.MessageReceivers.Count(r =>
                            r.ReceiverId == currentUserId &&
                            r.Message.SenderId ==
                                x.Message.SenderId &&
                            !r.IsRead)
                })

                .ToListAsync();

            var sentChats = await _context.Messages

                .Include(x => x.Receivers)

                .Where(x =>
                    x.SenderId == currentUserId
                )

                .SelectMany(x =>
                    x.Receivers.Select(r => new
                    {
                        employeeId =
                            r.Receiver.EmployeeId,

                        employeeName =
                            r.Receiver.EmployeeName,

                        lastMessage =
                            x.Body,

                        sentAt =
                            x.SentAt,

                        unreadCount = 0
                    })
                )

                .ToListAsync();

            var chatList = receivedChats

                .Concat(sentChats)

                .GroupBy(x => x.employeeId)

                .Select(g => g
                    .OrderByDescending(x => x.sentAt)
                    .First()
                )

                .OrderByDescending(x => x.sentAt)

                .Select(x => new
                {
                    employeeId = x.employeeId,

                    employeeName = x.employeeName,

                    lastMessage = x.lastMessage,

                    time = x.sentAt.ToString("dd MMM"),

                    unreadCount = x.unreadCount
                })

                .ToList();

            return Json(chatList);
        }
    }
}
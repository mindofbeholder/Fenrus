using Fenrus.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fenrus.Controllers;

/// <summary>
/// Controller for notes
/// </summary>
[Authorize]
[Route("notes")]
public class NotesController : BaseController
{
    /// <summary>
    /// Gets all the notes for the user
    /// </summary>
    /// <returns>the notes</returns>
    [HttpGet]
    public IEnumerable<object> GetAll([FromQuery] NoteType type, [FromQuery(Name = "db")] Guid dashboardUid)
    {
        var uid = User.GetUserUid().Value;
        if (type == NoteType.Media)
            return new MediaService().GetAll(uid);
        var notes = GetNotes(uid, type, dashboardUid);
        return notes.OrderBy(x => x.Order).Select(x => new
        {
            x.Uid,
            Name = EncryptionHelper.Decrypt(x.Name),
            Content = EncryptionHelper.Decrypt(x.Content)
        });
    }

    /// <summary>
    /// Gets the list of notes the user is accessing
    /// </summary>
    /// <param name="userUid">the UID of the user</param>
    /// <param name="type">the type of notes the user is accessing</param>
    /// <param name="dashboardUid">the UID of the dashboard the user is using</param>
    /// <returns>the list of notes</returns>
    List<Note> GetNotes(Guid userUid, NoteType type, Guid dashboardUid)
    {
        return new NotesService().GetAllByUser(type == NoteType.Shared ? Guid.Empty : userUid).Where(x =>
        {
            if (type == NoteType.Shared) return true;
            if (type == NoteType.Dashboard) return x.DashboardUid == dashboardUid;
            return x.DashboardUid == Guid.Empty;
        }).ToList();
    }

    /// <summary>
    /// Saves a note
    /// </summary>
    /// <param name="note">the note to save</param>
    /// <returns>the saved note</returns>
    [HttpPost]
    public Note SaveNote([FromBody] Note note, [FromQuery] NoteType type, [FromQuery(Name = "db")] Guid dashboardUid)
    {
        var uid = User.GetUserUid().Value;
        var service = new NotesService();
        if (note.Uid != Guid.Empty)
        {
            var existing = service.GetByUid(note.Uid);
            if (existing != null)
            {
                if (existing.UserUid != Guid.Empty && existing.UserUid != uid)
                    throw new UnauthorizedAccessException();
                if(existing.Name != note.Name)
                    existing.Name = EncryptionHelper.Encrypt(note.Name ?? string.Empty);
                if(existing.Content != note.Content)
                    existing.Content = EncryptionHelper.Encrypt(note.Content ?? string.Empty);
                service.Update(existing);
                return existing;
            }
        }

        note.UserUid = uid;
        
        if (note.Created < new DateTime(2020, 1, 1))
            note.Created = DateTime.UtcNow;
        
        note.Name = EncryptionHelper.Encrypt(note.Name ?? string.Empty);
        note.Content = EncryptionHelper.Encrypt(note.Content ?? string.Empty);
        if (type == NoteType.Shared)
            note.UserUid = Guid.Empty;
        else if (type == NoteType.Personal)
            note.DashboardUid = Guid.Empty;
        else
            note.DashboardUid = dashboardUid;

        if (note.Uid == Guid.Empty)
        {
            var items = GetNotes(uid, type, dashboardUid);
            note.Order = items.Any() ? items.Max(x => x.Order + 1) : 0;
            service.Add(note);
        }
        else
            service.Update(note);
        return note;
    }

    /// <summary>
    /// Uploads a media file
    /// </summary>
    /// <param name="file">the file being uploaded</param>
    /// <returns>the UID of the newly uploaded media</returns>
    [HttpPost("media")]
    public async Task<Guid> UploadMedia([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0 || string.IsNullOrWhiteSpace(file.FileName))
            throw new Exception("file not selected");

        var uid = User.GetUserUid().Value;
        
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Seek(0, SeekOrigin.Begin);
        var data = stream.ToArray();
        var guid = new MediaService().Add(uid, file.FileName, data);
        return guid.Value;
    }
    
    /// <summary>
    /// Deletes a note
    /// </summary>
    /// <param name="uid">the UID of the note</param>
    [HttpDelete("{uid}")]
    public void Delete([FromRoute] Guid uid, [FromQuery] NoteType type)
    {
        var userUid = User.GetUserUid().Value;

        if (type == NoteType.Media)
        {
            new MediaService().Delete(userUid, uid);
            return;
        }
        
        var service = new NotesService();
        var note = service.GetByUid(uid);
        if (note == null || (note.UserUid != Guid.Empty && note.UserUid != userUid))
            return;
        service.Delete(note.Uid);
    }
    
    /// <summary>
    /// Deletes a note
    /// </summary>
    /// <param name="uid">the UID of the note</param>
    [HttpPost("{uid}/move/{up}")]
    public bool Move([FromRoute] Guid uid, [FromRoute] bool up, [FromQuery] NoteType type, [FromQuery(Name = "db")] Guid dashboardUid)
    {
        var userUid = User.GetUserUid().Value;
        var service = new NotesService();
        var notes = GetNotes(userUid, type, dashboardUid);
        int index = notes.FindIndex(x => x.Uid == uid);
        if (index < 0)
            return false;  // unknown
        if (index == 0 && up)
            return false;
        if (index == notes.Count - 1 && up == false)
            return false;
        int dest = up ? index - 1 : index + 1;
        (notes[dest], notes[index]) = (notes[index], notes[dest]);
        for (int i = 0; i < notes.Count; i++)
        {
            if (notes[i].Order == i)
                continue;
            notes[i].Order = i;
            service.Update(notes[i]);
        }

        return true;
    }

    /// <summary>
    /// Note types
    /// </summary>
    public enum NoteType
    {
        /// <summary>
        /// Personal notes
        /// </summary>
        Personal,
        /// <summary>
        /// Dashboard notes
        /// </summary>
        Dashboard,
        /// <summary>
        /// Shared notes
        /// </summary>
        Shared,
        /// <summary>
        /// Media notes
        /// </summary>
        Media
    }
}
// BotMobile FB helper - port dari Bot_Ngekeng core/fb_client.py _HELPER_JS (subset GraphQL).
// Di-inject via EvaluateFunctionOnNewDocument; semua fungsi return JSON.stringify(result).
// doc_id dari Bot_Ngekeng (bisa expire - ubah di sini saja, konstanta FbDocIds.cs juga ada untuk C#).
(() => {
  if (window.__mfb && window.__mfb._installed) return;
  const M = {};

  // ---------- token scraping (port getTokensFromPage + hasil probe mobile web) ----------
  // mobile web (m.facebook): TIDAK pakai fb_dtsg — cukup lsd + jazoest (input form).
  // desktop www (comet): fb_dtsg dari DTSGInitialData/require/input/regex.
  M.getTokens = () => {
    const out = { fb_dtsg: '', lsd: '', jazoest: '', userId: '0' };
    try { out.fb_dtsg = (window.DTSGInitialData && window.DTSGInitialData.token) || ''; } catch (e) {}
    if (!out.fb_dtsg) {
      try { out.fb_dtsg = window.require('DTSGInitData').token; } catch (e) {}
    }
    if (!out.fb_dtsg) {
      const el = document.querySelector('input[name="fb_dtsg"]');
      if (el) out.fb_dtsg = el.value;
    }
    if (!out.fb_dtsg) {
      const m = document.documentElement.outerHTML.match(/"DTSGInitData".*?"token".*?"([^"]+)"/);
      if (m) out.fb_dtsg = m[1];
    }
    try { out.lsd = (window.LSD && window.LSD.token) || ''; } catch (e) {}
    if (!out.lsd) {
      const el = document.querySelector('input[name="lsd"]');
      if (el) out.lsd = el.value;
    }
    if (!out.lsd) {
      const m = document.documentElement.outerHTML.match(/"LSD",\[\],\{"token":"([^"]+)"/);
      if (m) out.lsd = m[1];
    }
    // jazoest: input form (mobile web) → fallback hitung dari fb_dtsg (desktop)
    const jz = document.querySelector('input[name="jazoest"]');
    out.jazoest = jz && jz.value ? jz.value
      : (out.fb_dtsg ? '2' + String(out.fb_dtsg.split('').reduce((a, c) => a + c.charCodeAt(0), 0)) : '');
    try { out.userId = String(window.CurrentUserInitialData.USER_ID || '0'); } catch (e) {}
    if (out.userId === '0') {
      const m = document.cookie.match(/c_user=(\d+)/);
      if (m) out.userId = m[1];
    }
    if (out.userId === '0') {
      const m = document.documentElement.outerHTML.match(/"USER_ID":"(\d+)"/);
      if (m) out.userId = m[1];
    }
    return out;
  };

  const BASE = () => location.origin.indexOf('facebook.com') >= 0
    ? 'https://www.facebook.com' : location.origin;

  // ---------- graphql fetch wrapper ----------
  M.graphql = async (docId, friendlyName, variables) => {
    const t = M.getTokens();
    // mobile web: lsd cukup (fb_dtsg tak ada); desktop: fb_dtsg wajib
    if (!t.lsd || t.userId === '0') return JSON.stringify({ __error: 'no_tokens', tokens: t });
    const body = new URLSearchParams();
    body.set('av', t.userId);
    body.set('__user', t.userId);
    body.set('__a', '1');
    if (t.fb_dtsg) body.set('fb_dtsg', t.fb_dtsg);
    body.set('lsd', t.lsd);
    if (t.jazoest) body.set('jazoest', t.jazoest);
    body.set('fb_api_caller_class', 'RelayModern');
    body.set('fb_api_req_friendly_name', friendlyName);
    body.set('variables', JSON.stringify(variables));
    body.set('doc_id', docId);
    body.set('server_timestamps', 'true');
    const ctrl = new AbortController();
    const timer = setTimeout(() => ctrl.abort(), 20000);
    try {
      const r = await fetch(BASE() + '/api/graphql/', {
        method: 'POST',
        credentials: 'include',
        headers: {
          'content-type': 'application/x-www-form-urlencoded',
          'x-fb-lsd': t.lsd,
          'x-fb-friendly-name': friendlyName,
          'x-asbd-id': '129477',
        },
        body: body.toString(),
        signal: ctrl.signal,
      });
      const txt = (await r.text()).replace(/^for\(;;\);/, '');
      return JSON.stringify({ status: r.status, body: txt.slice(0, 20000) });
    } catch (e) {
      return JSON.stringify({ __error: 'fetch_failed', message: String(e) });
    } finally {
      clearTimeout(timer);
    }
  };

  const jsonParse = (s) => { try { return JSON.parse(s); } catch (e) { return null; } };
  const send = async (res) => {
    const o = jsonParse(res);
    if (!o) return { ok: false, outcome: 'parse_failure' };
    if (o.__error) return { ok: false, outcome: o.__error };
    return { ok: true, status: o.status, raw: o.body || '' };
  };

  // ---------- add friend (3 varian payload, port fb_client.addFriend) ----------
  const ADD_FRIEND_DOCS = ['9012643805460802'];
  M.addFriend = async (targetUid) => {
    const t = M.getTokens();
    const now = Date.now();
    const variants = [
      {
        doc: ADD_FRIEND_DOCS[0],
        variables: {
          input: {
            attribution_id_v2: `FriendingCometRoot.react,comet.friending,friends_home_main,${now},1234567890,,,`,
            friend_requestee_ids: [String(targetUid)],
            friending_channel: 'FRIENDS_HOME_MAIN',
            people_you_may_know_location: 'friends_center',
            warn_ack_for_ids: [],
            actor_id: t.userId,
            client_mutation_id: String(Math.floor(Math.random() * 10000)),
          },
          scale: 1,
        },
      },
      {
        doc: ADD_FRIEND_DOCS[0],
        variables: {
          input: {
            attribution_id_v2: `FriendingCometProfileFriendButton.react,comet.friending,profile_page,${now},1234567890,,,`,
            friend_requestee_ids: [String(targetUid)],
            friending_channel: 'PROFILE_PAGE',
            people_you_may_know_location: 'profile_toggle',
            warn_ack_for_ids: [],
            actor_id: t.userId,
            client_mutation_id: String(Math.floor(Math.random() * 10000)),
          },
          scale: 1,
        },
      },
      {
        doc: ADD_FRIEND_DOCS[0],
        variables: {
          client_mutation_id: String(Math.floor(Math.random() * 10000)),
          actor_id: t.userId,
          friend_id: String(targetUid),
          source: 'friends_home_main',
        },
      },
    ];
    const outcomes = [];
    for (const v of variants) {
      const res = await send(await M.graphql(v.doc, 'FriendingCometFriendRequestSendMutation', v.variables));
      const blob = res.raw || '';
      if (blob.indexOf('OUTGOING_REQUEST') >= 0 || blob.indexOf('friend_request_send') >= 0)
        return JSON.stringify({ ok: true, outcome: 'request_sent', variant: outcomes.length });
      if (blob.indexOf('ARE_FRIENDS') >= 0)
        return JSON.stringify({ ok: false, outcome: 'already_friend' });
      if (blob.indexOf('CANNOT_REQUEST') >= 0 || blob.indexOf('blocked') >= 0)
        return JSON.stringify({ ok: false, outcome: 'blocked_or_cannot_request' });
      if (blob.indexOf('1357004') >= 0 || /rate limit|too many/i.test(blob))
        return JSON.stringify({ ok: false, outcome: 'rate_limit' });
      if (/1357001|1357053|log in|checkpoint/i.test(blob))
        return JSON.stringify({ ok: false, outcome: 'session_expired' });
      outcomes.push(blob.slice(0, 120));
    }
    return JSON.stringify({ ok: false, outcome: 'unknown_response', previews: outcomes });
  };

  // ---------- pending friend requests (port fetchPendingFriendRequests) ----------
  M.fetchPendingRequests = async () => {
    const res = await send(await M.graphql('4499082396829105', 'FriendingCometRootQuery',
      { requests_initial: 1000, scale: 1 }));
    if (!res.ok) return JSON.stringify(res);
    const data = jsonParse(res.raw);
    const out = [];
    try {
      const edges = data.data.viewer.friending_possibilities.edges || [];
      for (const e of edges) {
        if (e.node && e.node.friendship_status === 'INCOMING_REQUEST' && e.node.id)
          out.push(String(e.node.id));
      }
    } catch (err) {}
    return JSON.stringify({ ok: true, uids: out });
  };

  // ---------- confirm friend request (port confirmFriendRequest) ----------
  const CONFIRM_DOCS = ['27260433676892385', '4379690545439556'];
  M.confirmFriendRequest = async (requesterUid) => {
    const t = M.getTokens();
    for (const doc of CONFIRM_DOCS) {
      const res = await send(await M.graphql(doc, 'FriendingCometFriendRequestConfirmMutation', {
        input: {
          click_correlation_id: String(Date.now()),
          click_proof_validation_result: '{"validated":true}',
          friend_requester_id: String(requesterUid),
          friending_channel: 'FRIENDS_HOME_REQUESTS',
          warn_ack: false,
          actor_id: t.userId,
          client_mutation_id: String(Math.floor(Math.random() * 10000)),
        },
        scale: 1,
        refresh_num: 0,
        should_fix_banner: true,
      }));
      const blob = res.raw || '';
      if (blob.indexOf('ARE_FRIENDS') >= 0)
        return JSON.stringify({ ok: true, outcome: 'confirmed' });
      if (blob.indexOf('CAN_REQUEST') >= 0)
        return JSON.stringify({ ok: false, outcome: 'request_cancelled' });
      if (/1357001|1357053|log in|checkpoint/i.test(blob))
        return JSON.stringify({ ok: false, outcome: 'session_expired' });
    }
    return JSON.stringify({ ok: false, outcome: 'unknown_response' });
  };

  // ---------- fetch friends (port fetch_friends) ----------
  M.fetchFriends = async () => {
    const res = await send(await M.graphql('29498081956473146', 'FriendingCometFriendsListPaginationQuery',
      { count: 50, cursor: null, scale: 1, name: null }));
    if (!res.ok) return JSON.stringify(res);
    const data = jsonParse(res.raw);
    const out = [];
    try {
      const edges = data.data.viewer.friending_possibilities.edges || [];
      for (const e of edges) if (e.node && e.node.id) out.push(String(e.node.id));
    } catch (err) {}
    return JSON.stringify({ ok: true, uids: out });
  };

  // ---------- create group (port createGroup) ----------
  const findThreadId = (obj) => {
    const stack = [obj];
    while (stack.length) {
      const cur = stack.pop();
      if (!cur || typeof cur !== 'object') continue;
      if (cur.thread_key && cur.thread_key.thread_fbid) return cur.thread_key.thread_fbid;
      for (const k of Object.keys(cur)) stack.push(cur[k]);
    }
    return null;
  };
  M.createGroup = async (groupName) => {
    const t = M.getTokens();
    const res = await send(await M.graphql('577041672419534', 'MessengerGroupCreateMutation', {
      input: {
        client_mutation_id: '1',
        actor_id: t.userId,
        participants: [{ fbid: t.userId }],
        thread_settings: { name: groupName, joinable_mode: 'PRIVATE', thread_image_fbid: null },
        entry_point: 'chat_sidebar_new_group',
      },
    }));
    if (!res.ok) return JSON.stringify(res);
    const data = jsonParse(res.raw);
    const tid = data ? findThreadId(data) : null;
    return JSON.stringify({ ok: !!tid, outcome: tid ? 'created' : 'missing_thread_id', thread_id: tid });
  };

  // ---------- post status text (port createPostWithPhoto, tanpa foto) ----------
  M.postStatus = async (message, privacy) => {
    const t = M.getTokens();
    const input = {
      composer_entry_point: 'inline_composer',
      composer_source_surface: 'timeline',
      idempotence_token: t.userId + '_FEED',
      source: 'WWW',
      attachments: [],
      audience: { privacy: { allow: [], base_state: privacy || 'EVERYONE', deny: [], tag_expansion_state: 'UNSPECIFIED' } },
      message: { text: message },
      actor_id: t.userId,
      client_mutation_id: '1',
    };
    const res = await send(await M.graphql('26200680759550052', 'ComposerStoryCreateMutation', {
      input,
      feedLocation: 'TIMELINE',
      privacySelectorRenderLocation: 'COMET_STREAM',
      renderLocation: 'timeline',
      isTimeline: true,
      isLegacyActivePostPrivacyDialog: false,
    }));
    if (!res.ok) return JSON.stringify(res);
    const blob = res.raw || '';
    if (/story_create|story_update|story_publish/.test(blob)) {
      const data = jsonParse(blob);
      let postId = null;
      try { postId = data.data.story_create.story.legacy_story_hideable_id || null; } catch (e) {}
      return JSON.stringify({ ok: true, outcome: 'posted', post_id: postId });
    }
    if (blob.indexOf('1357031') >= 0) return JSON.stringify({ ok: false, outcome: 'restricted' });
    if (/1357001|1357053|log in|checkpoint/i.test(blob))
      return JSON.stringify({ ok: false, outcome: 'session_expired' });
    return JSON.stringify({ ok: false, outcome: 'unknown_response', preview: blob.slice(0, 150) });
  };

  // ---------- set bio text (port set_bio_text) ----------
  M.setBioText = async (bio) => {
    const t = M.getTokens();
    const res = await send(await M.graphql('26634540449575467', 'ProfileCometSetBioMutation', {
      input: { bio, publish_bio_feed_story: false, actor_id: t.userId, client_mutation_id: '1' },
      hasProfileTileViewID: false,
      profileTileViewID: null,
      scale: 1,
      useDefaultActor: false,
    }));
    if (!res.ok) return JSON.stringify(res);
    const blob = res.raw || '';
    if (blob.indexOf('profile_intro_card_set') >= 0 || blob.indexOf(bio.slice(0, 32)) >= 0)
      return JSON.stringify({ ok: true, outcome: 'bio_set' });
    return JSON.stringify({ ok: false, outcome: 'bio_not_confirmed', preview: blob.slice(0, 150) });
  };

  // ---------- enable professional mode (port activateProMode) ----------
  M.activateProMode = async () => {
    const res = await send(await M.graphql('10032435873458768', 'CometProModeActivationDialogTransitionMutation',
      { category_id: '2347428775505624', surface: 'PERMANENT_ENTRY' }));
    if (!res.ok) return JSON.stringify(res);
    const blob = res.raw || '';
    if (blob.indexOf('"profile_plus_mutation":null') >= 0 && blob.indexOf('"errors"') >= 0)
      return JSON.stringify({ ok: false, outcome: 'server_rejected', preview: blob.slice(0, 200) });
    if (blob.indexOf('profile_plus_mutation') >= 0)
      return JSON.stringify({ ok: true, outcome: 'activated' });
    if (blob.indexOf('1357031') >= 0) return JSON.stringify({ ok: false, outcome: 'restricted' });
    return JSON.stringify({ ok: false, outcome: 'ambiguous_no_error', preview: blob.slice(0, 150) });
  };

  // ---------- PYMK suggestions (port fetch_suggestions_graphql) ----------
  M.fetchSuggestions = async (count) => {
    const locations = ['FRIENDS_CENTER', 'FRIENDS_HOME_MAIN', 'COMET_FRIENDS_PAGE', 'NEWSFEED_PYMK'];
    for (const loc of locations) {
      const res = await send(await M.graphql('24534454102821334', 'FriendingCometPYMKPanelPaginationQuery',
        { count: count || 50, cursor: null, scale: 1, name: null, location: loc }));
      if (!res.ok) return JSON.stringify(res);
      const data = jsonParse(res.raw);
      const out = [];
      try {
        const edges = data.data.viewer.friending_possibilities.edges || [];
        for (const e of edges) {
          if (e.node && e.node.id && e.node.friendship_status === 'CAN_REQUEST')
            out.push(String(e.node.id));
        }
      } catch (err) {}
      if (out.length) return JSON.stringify({ ok: true, uids: out, location: loc });
    }
    return JSON.stringify({ ok: false, outcome: 'no_suggestions' });
  };

  // ---------- upload binary (port uploadStoryPhoto pattern: token di FormData) ----------
  M.uploadPhotoGeneric = async (base64, mime, fileName, purpose) => {
    const t = M.getTokens();
    if (!t.fb_dtsg) return JSON.stringify({ ok: false, outcome: 'no_lsd_token' });
    const urls = purpose === 'profile_pic'
      ? [`${BASE()}/profile/picture/upload/?profile_id=${t.userId}&photo_source=57`]
      : [
        'https://upload.facebook.com/ajax/react_composer/attachments/photo/upload',
        `${BASE()}/ajax/react_composer/attachments/photo/upload`,
        `${BASE()}/ajax/composer/attachments/photo/upload`,
      ];
    const blob = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
    for (const url of urls) {
      const fd = new FormData();
      if (purpose === 'story') {
        fd.append('source', '8');
        fd.append('profile_id', t.userId);
        fd.append('waterfallxapp', 'comet_stories');
      } else if (purpose === 'profile_pic') {
        fd.append('profile_id', t.userId);
        fd.append('photo_source', '57');
      } else {
        fd.append('source', '8');
      }
      fd.append('av', t.userId);
      fd.append('__user', t.userId);
      fd.append('__a', '1');
      fd.append('fb_dtsg', t.fb_dtsg);
      fd.append('lsd', t.lsd);
      fd.append('jazoest', t.jazoest);
      fd.append('farr', new Blob([blob], { type: mime }), fileName);
      const ctrl = new AbortController();
      const timer = setTimeout(() => ctrl.abort(), 30000);
      try {
        const r = await fetch(url, { method: 'POST', credentials: 'include', body: fd, signal: ctrl.signal });
        const txt = (await r.text()).replace(/^for\(;;\);/, '');
        const data = jsonParse(txt);
        const p = data && data.payload ? data.payload : {};
        const pid = p.fbid || p.photo_id || p.id || p.photoID || p.image_id || p.aId || null;
        if (pid) return JSON.stringify({ ok: true, photo_id: String(pid), url: url.split('?')[0] });
      } catch (e) {
        // lanjut URL berikutnya
      } finally {
        clearTimeout(timer);
      }
    }
    return JSON.stringify({ ok: false, outcome: 'upload_failed_all_endpoints' });
  };

  // ---------- create story (port createStory) ----------
  M.createStory = async (photoId) => {
    const t = M.getTokens();
    const res = await send(await M.graphql('26770527039211553', 'StoriesCreateMutation', {
      input: {
        audiences: [{ stories: { self: { target_id: t.userId } } }],
        audiences_is_complete: true,
        composer_type: 'story',
        composer_source_surface: 'newsfeed',
        composer_entry_point: 'inline_composer',
        attachments: [{ photo: { id: String(photoId), overlays: [] } }],
        actor_id: t.userId,
        client_mutation_id: 'ms' + Date.now(),
      },
    }));
    if (!res.ok) return JSON.stringify(res);
    const blob = res.raw || '';
    if (blob.indexOf('story_create') >= 0) {
      const data = jsonParse(blob);
      let sid = 'unknown';
      try {
        const sc = data.data.story_create;
        sid = (sc.items && sc.items[0] && sc.items[0].story && sc.items[0].story.id)
          || (sc.story && sc.story.id) || 'unknown';
      } catch (e) {}
      return JSON.stringify({ ok: true, outcome: 'story_created', story_id: String(sid) });
    }
    return JSON.stringify({ ok: false, outcome: 'unknown_response', preview: blob.slice(0, 150) });
  };

  // ---------- set story privacy (port setStoryPrivacy) ----------
  M.setStoryPrivacy = async (mode) => {
    const t = M.getTokens();
    const res = await send(await M.graphql('26547817461576340', 'StoriesCometPrivacySelectorAudienceModeMutation',
      { input: { audience_mode: mode, actor_id: t.userId, client_mutation_id: '1' } }));
    const blob = res.raw || '';
    if (blob.indexOf('audience_mode') >= 0 && blob.indexOf('"errors"') < 0)
      return JSON.stringify({ ok: true, outcome: 'privacy_set' });
    return JSON.stringify({ ok: false, outcome: 'privacy_not_set', preview: blob.slice(0, 120) });
  };

  // ---------- set profile pic (port setProfilePic) ----------
  M.setProfilePic = async (photoId, caption) => {
    const t = M.getTokens();
    const res = await send(await M.graphql('9015637238455590', 'ProfileCometProfilePictureSetMutation', {
      input: {
        caption: caption || '',
        existing_photo_id: String(photoId),
        profile_id: t.userId,
        profile_pic_method: 'EXISTING',
        profile_pic_source: 'WELCOME',
        scaled_crop_rect: { height: 1, width: 1, x: 0, y: 0 },
        skip_cropping: true,
        actor_id: t.userId,
        client_mutation_id: '1',
      },
      isPage: false,
      isProfile: true,
      scale: 1,
    }));
    const blob = res.raw || '';
    if (blob.indexOf('profile_picture_set') >= 0 || blob.indexOf('profilePicture') >= 0)
      return JSON.stringify({ ok: true, outcome: 'profile_pic_set' });
    return JSON.stringify({ ok: false, outcome: 'set_failed', preview: blob.slice(0, 150) });
  };

  M._installed = true;
  window.__mfb = M;
})();

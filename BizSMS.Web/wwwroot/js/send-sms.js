// Port legacy Scripts/Custom/send.sms.js na Bootstrap 5 (bez bootbox/jquery-ui/datetimepicker).
// Vodi 4-koračni čarobnjak u Views/Home/SendSMS.cshtml i priča sa /api/Home/* (HomeApiController).
// JSON ključevi su PascalCase (Program.cs PropertyNamingPolicy = null) — isto kao u legacy-ju.
(function () {
    "use strict";

    var groupSendMode = false; // true = slanje celoj grupi; false = izabrani brojevi

    // ---------- Navigacija kroz korake ----------
    function goToStep(n) {
        $(".wizard-step").addClass("d-none");
        $('.wizard-step[data-step="' + n + '"]').removeClass("d-none");
        $('#step-indicator .nav-link').removeClass("active").addClass("disabled");
        $('#step-indicator .nav-link[data-step="' + n + '"]').removeClass("disabled").addClass("active");

        if (n >= 3) refreshSummaries();
    }

    $(".wizard-prev").on("click", function () {
        var cur = parseInt($(this).closest(".wizard-step").attr("data-step"), 10);
        if (cur > 1) goToStep(cur - 1);
    });

    // ---------- Pomoćne ----------
    function checkCoding(msg) {
        for (var i = 0; i < msg.length; i++) {
            if (msg.charCodeAt(i) > 127) return 1; // Unicode (66 kar/segment)
        }
        return 0; // GSM-7 (160 kar/segment)
    }

    function computeLength(msg) {
        return checkCoding(msg) === 1 ? Math.ceil(msg.length / 66) : Math.ceil(msg.length / 160);
    }

    function currentAlphanumeric() {
        return $("#alphanumericDropdown option:selected").text();
    }

    function selectedNumberIds() {
        return $("#selected-numbers option").map(function () { return $(this).val(); }).get();
    }

    function alertRedirect(message) {
        window.alert(message);
        window.location.href = "/Home/Index";
    }

    // ---------- Korak 1: grupe i brojevi ----------
    function loadGroups() {
        $.ajax({ url: "/api/Home/GetGroups", method: "GET" })
            .done(function (data) {
                var select = $("#group_select").empty();
                $.each(data, function (i, g) {
                    select.append($("<option>").val(g.Value).text(g.Text));
                });
                select.off("change").on("change", loadNumbersForGroup);
                if (data.length > 0) select.trigger("change");
            })
            .fail(function (xhr) {
                if (xhr.status === 401) return;
                window.alert("Greška pri učitavanju grupa.");
            });
    }

    function loadNumbersForGroup() {
        var groupId = $("#group_select").val();

        $.ajax({ url: "/api/Home/GetNumbers/" + groupId, method: "GET" })
            .done(function (data) {
                var avail = $("#available-numbers").empty();
                var alreadySelected = selectedNumberIds();

                $.each(data, function (i, n) {
                    if (alreadySelected.indexOf(String(n.NumberID)) !== -1) return;
                    var text = n.Number + (n.Name ? " (" + n.Name + ")" : "");
                    avail.append($("<option>").val(n.NumberID).text(text));
                });

                $("#btn-send-to-group").prop("disabled", data.length === 0);
            })
            .fail(function (xhr) {
                if (xhr.status === 401) return;
                window.alert("Greška pri učitavanju brojeva.");
            });

        // Test broj (broj ulogovanog korisnika)
        $.ajax({ url: "/api/Home/GetTestNumber", method: "GET" })
            .done(function (data) { $("#test-number").text(data); })
            .fail(function (xhr) { if (xhr.status !== 401) $("#test-number").text(""); });
    }

    $("#btn-add-numbers").on("click", function () {
        $("#available-numbers option:selected").each(function () {
            $(this).prop("selected", false).appendTo("#selected-numbers");
        });
        updateStep1Next();
    });

    $("#btn-remove-numbers").on("click", function () {
        $("#selected-numbers option:selected").each(function () {
            $(this).prop("selected", false).appendTo("#available-numbers");
        });
        updateStep1Next();
    });

    function updateStep1Next() {
        var hasSelected = $("#selected-numbers option").length > 0;
        $("#btn-step1-next").prop("disabled", !hasSelected);
    }

    // Best-effort filter (option.hidden podržan u modernim browserima)
    function wireFilter(inputId, selectId) {
        $("#" + inputId).on("keyup", function () {
            var val = $.trim($(this).val()).toLowerCase();
            $("#" + selectId + " option").each(function () {
                var match = $(this).text().toLowerCase().indexOf(val) !== -1;
                this.hidden = !match;
            });
        });
    }
    wireFilter("filter-available", "available-numbers");
    wireFilter("filter-selected", "selected-numbers");

    // ---------- Prelazak sa koraka 1 ----------
    function proceedFromStep1() {
        var dataRequest = groupSendMode
            ? { NumbersToCheck: null, GroupId: $("#group_select").val() }
            : { NumbersToCheck: selectedNumberIds(), GroupId: null };

        $.ajax({
            contentType: "application/json",
            method: "POST",
            data: JSON.stringify(dataRequest),
            url: "/api/Home/CheckNonMtsGetAlphanums"
        })
            .done(function (data) {
                var dd = $("#alphanumericDropdown").empty();
                $.each(data, function (i, a) {
                    dd.append($("<option>").val(a.Value).text(a.Text));
                });
                setAlphaDisplays(data.length ? data[0].Text : "");
                dd.off("change").on("change", function () {
                    setAlphaDisplays(currentAlphanumeric());
                    updateStep2Next();
                });
                updateStep2Next();
                goToStep(2);
            })
            .fail(function (xhr) {
                if (xhr.status === 401) return;
                window.alert(xhr.responseText || "Greška pri proveri pošiljalaca.");
            });

        loadUnsubTexts();
    }

    function setAlphaDisplays(text) {
        $(".alpha-display").text(text);
    }

    function loadUnsubTexts() {
        $.ajax({ url: "/api/Home/GetUnsubTextMts", method: "GET" })
            .done(function (d) { $("#inMts").text(d); })
            .fail(function () { $("#inMts").text(""); });
        $.ajax({ url: "/api/Home/GetUnsubtextNotInMts", method: "GET" })
            .done(function (d) { $("#notInMts").text(d); })
            .fail(function () { $("#notInMts").text(""); });
    }

    $("#btn-step1-next").on("click", function () {
        if ($(this).prop("disabled")) return;
        groupSendMode = false;
        proceedFromStep1();
    });

    $("#btn-send-to-group").on("click", function () {
        if ($(this).prop("disabled")) return;
        groupSendMode = true;
        proceedFromStep1();
    });

    // ---------- Korak 2: poruka ----------
    function updateStep2Next() {
        var ok = $("#message").val().length > 0 && $("#alphanumericDropdown option").length > 0;
        $("#btn-step2-next").prop("disabled", !ok);
    }

    $("#message").on("keyup input", function () {
        var msg = $(this).val();
        if (checkCoding(msg) === 1) {
            $(this).attr("maxlength", "315");
            $("#charCount").text(msg.length + "/66 (" + Math.ceil(msg.length / 66) + ")");
        } else {
            $(this).attr("maxlength", "765");
            $("#charCount").text(msg.length + "/160 (" + Math.ceil(msg.length / 160) + ")");
        }
        updateStep2Next();
    });

    $("#btn-step2-next").on("click", function () {
        if ($(this).prop("disabled")) return;
        goToStep(3);
    });

    // ---------- Korak 3: test ----------
    function refreshSummaries() {
        var msg = $("#message").val();
        $(".message-display").text(msg);
        setAlphaDisplays(currentAlphanumeric());

        // Rezime na koraku 4
        if (groupSendMode) {
            $("#send-summary").text("Poruka se šalje celoj grupi:");
            $("#final-numbers-wrap").addClass("d-none");
            $("#final-group-wrap").removeClass("d-none");
            $("#final-group-name").text($("#group_select option:selected").text());
        } else {
            $("#send-summary").text("Poruka se šalje izabranim brojevima:");
            $("#final-group-wrap").addClass("d-none");
            $("#final-numbers-wrap").removeClass("d-none");
            var html = $("#selected-numbers option").map(function () {
                return $("<div>").text($(this).text()).html();
            }).get().join("<br />");
            $("#final-numbers-list").html(html);
        }
    }

    $("#btn-send-test").on("click", function () {
        var testNumber = $("#test-number").text();
        var alphanumeric = currentAlphanumeric();
        var message = $("#message").val();

        var dataRequest = {
            PhoneNumber: testNumber,
            Alphanumeric: alphanumeric,
            Message: message,
            MessageLength: computeLength(message)
        };

        var modalEl = document.getElementById("testModal");
        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();

        $.ajax({
            contentType: "application/json",
            method: "POST",
            data: JSON.stringify(dataRequest),
            url: "/api/Home/SendTestSMS"
        })
            .done(function () {
                // Kratko sačekaj (dostava), pa pitaj da li je test stigao.
                setTimeout(function () {
                    modal.hide();
                    if (window.confirm("Da li ste primili test poruku?")) {
                        $("#btn-skip-test").addClass("d-none");
                        $("#btn-step3-next").removeClass("d-none").prop("disabled", false);
                    }
                }, 3000);
            })
            .fail(function () {
                modal.hide();
                window.alert("Slanje test poruke nije uspelo.");
            });
    });

    $("#btn-skip-test").on("click", function () { goToStep(4); });
    $("#btn-step3-next").on("click", function () { goToStep(4); });

    // ---------- Korak 4: slanje ----------
    function disableSendButtons() {
        $("#btn-send, #btn-schedule").prop("disabled", true);
    }
    function enableSendButtons() {
        $("#btn-send, #btn-schedule").prop("disabled", false);
    }

    function buildSendRequest(scheduledDateTime) {
        var message = $("#message").val();
        var common = {
            Alphanumeric: currentAlphanumeric(),
            Message: message,
            MessageLength: computeLength(message)
        };
        if (scheduledDateTime) common.ScheduledDateTime = scheduledDateTime;

        if (groupSendMode) {
            common.GroupId = $("#group_select").val();
            return { url: "/api/Home/SendGroupSMS", body: common };
        }
        common.PhoneNumbers = selectedNumberIds();
        return { url: "/api/Home/SendSMS", body: common };
    }

    function postSend(req) {
        disableSendButtons();
        $.ajax({
            contentType: "application/json",
            method: "POST",
            data: JSON.stringify(req.body),
            url: req.url
        })
            .done(function (data) {
                if (data && data.message === "OK") {
                    alertRedirect("Poruka je uspešno prosleđena za slanje.");
                } else {
                    alertRedirect("Došlo je do greške pri slanju poruke.");
                }
            })
            .fail(function (xhr) {
                enableSendButtons();
                window.alert(xhr.responseText || "Došlo je do greške pri slanju poruke.");
            });
    }

    $("#btn-send").on("click", function () {
        postSend(buildSendRequest(null));
    });

    $("#btn-schedule").on("click", function () {
        var dt = $("#schedule-datetime").val();
        if (!dt) {
            window.alert("Unesite datum i vreme zakazivanja.");
            return;
        }
        if (new Date(dt) < new Date()) {
            window.alert("Zakazano vreme mora biti u budućnosti.");
            return;
        }
        postSend(buildSendRequest(dt));
    });

    // ---------- Init ----------
    $(function () {
        loadGroups();
        updateStep1Next();
        goToStep(1);
    });
})();
